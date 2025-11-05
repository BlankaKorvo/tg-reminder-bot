
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using TimeZoneConverter;
using TgReminderBot.Data;
using TgReminderBot.Jobs;
using TgReminderBot.Models;

namespace TgReminderBot.Services;

public interface ISchedulingService
{
    Task UpsertAndReschedule(Reminder r, string defaultTz, string? tag = null, CancellationToken ct = default);
    Task DeleteAndUnschedule(string id, CancellationToken ct = default);
    Task RescheduleAll(AppDbContext db, string defaultTz, CancellationToken ct = default);
}

public sealed class SchedulingService : ISchedulingService
{
    private readonly ISchedulerFactory _factory;
    private readonly ILogger<SchedulingService> _log;

    public SchedulingService(ISchedulerFactory factory, ILogger<SchedulingService> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task RescheduleAll(AppDbContext db, string defaultTz, CancellationToken ct = default)
    {
        var sch = await _factory.GetScheduler(ct);

        // Clean all reminder triggers
        var all = await sch.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupStartsWith("reminder"), ct);
        foreach (var tk in all) await sch.UnscheduleJob(tk, ct);

        var reminders = await db.Reminders.AsNoTracking().ToListAsync(ct);
        foreach (var r in reminders)
        {
            try { await UpsertAndReschedule(r, defaultTz, null, ct); }
            catch (Exception ex) { _log.LogError(ex, "Failed to reschedule reminder {Id}", r.Id); }
        }
    }

    public async Task UpsertAndReschedule(Reminder r, string defaultTz, string? tag = null, CancellationToken ct = default)
    {
        var sch = await _factory.GetScheduler(ct);

        // Remove previous triggers for this reminder
        var existing = await sch.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupEndsWith($":{r.Id}"), ct);
        foreach (var tk in existing) await sch.UnscheduleJob(tk, ct);

        // Normalize timezone
        var tzId = string.IsNullOrWhiteSpace(r.TimeZone) ? defaultTz : r.TimeZone.Trim();
        TimeZoneInfo tz;
        try { tz = TZConvert.GetTimeZoneInfo(tzId); }
        catch { tz = TimeZoneInfo.Utc; }

        // Ensure durable job exists
        var jobKey = new JobKey($"reminders.reminder:{r.Id}", "reminders");
        if (!await sch.CheckExists(jobKey, ct))
        {
            await sch.AddJob(JobBuilder.Create<SendTelegramMessageJob>()
                .WithIdentity(jobKey)
                .StoreDurably()
                .Build(), true, ct);
        }

        // Base job data — strings only
        var baseData = new JobDataMap
        {
            ["chatId"] = r.ChatId.ToString(CultureInfo.InvariantCulture),
            ["text"] = r.Text ?? string.Empty,
            ["title"] = r.Text ?? string.Empty,
            ["parseMode"] = r.ParseMode ?? string.Empty,
            ["noPreview"] = r.NoPreview ? "true" : "false",
            ["_tag"] = tag ?? string.Empty
        };
        if (r.ThreadId is int threadId)
            baseData["threadId"] = threadId.ToString(CultureInfo.InvariantCulture);

        var triggers = new List<ITrigger>();

        // RunAt: one-off text reminder
        if (!string.IsNullOrWhiteSpace(r.RunAt) && TryParseLocalOrOffset(r.RunAt!, tz, out var runAtUtc))
        {
            if (runAtUtc > DateTimeOffset.UtcNow)
            {
                var data = CloneJobData(baseData);
                triggers.Add(TriggerBuilder.Create()
                    .WithIdentity(new TriggerKey($"reminder.single.runat:{r.Id}", "reminder.single"))
                    .ForJob(jobKey)
                    .UsingJobData(data)
                    .StartAt(runAtUtc.UtcDateTime)
                    .Build());
            }
        }

        // Cron
        if (!string.IsNullOrWhiteSpace(r.Cron))
        {
            try
            {
                var cron = r.Cron!.Trim();
                var data = CloneJobData(baseData);
                triggers.Add(TriggerBuilder.Create()
                    .WithIdentity(new TriggerKey($"reminder.cron:{r.Id}", "reminder.cron"))
                    .ForJob(jobKey)
                    .UsingJobData(data)
                    .WithCronSchedule(cron, x =>
                    {
                        x.InTimeZone(tz);
                        x.WithMisfireHandlingInstructionDoNothing();
                    })
                    .Build());
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Invalid cron for reminder {Id}: {Cron}", r.Id, r.Cron);
            }
        }

        // EventAt + RemindOffsets (+ optional poll on last-before)
        if (!string.IsNullOrWhiteSpace(r.EventAt) && TryParseLocalOrOffset(r.EventAt!, tz, out var eventUtc))
        {
            var offsets = ParseOffsets(r.RemindOffsets);
            var wantPoll = HasPollFlag(r.RemindOffsets) || (tag?.Contains("poll", StringComparison.OrdinalIgnoreCase) ?? false);

            // default (if no offsets) — notification at event time
            if (offsets.Count == 0) offsets.Add(TimeSpan.Zero);

            // last-before = the closest negative offset to zero
            var negatives = offsets.Where(o => o < TimeSpan.Zero).ToList();
            var lastBefore = negatives.Count > 0 ? negatives.Max() : (TimeSpan?)null;

            foreach (var off in offsets)
            {
                var when = eventUtc + off;
                if (when <= DateTimeOffset.UtcNow) continue;

                var data = CloneJobData(baseData);
                data["timeLeftSec"] = Math.Abs(off.TotalSeconds).ToString(CultureInfo.InvariantCulture);

                // if poll requested and this is the last negative reminder (not zero) — send poll instead of text
                var isPollHere = wantPoll && lastBefore.HasValue && off == lastBefore.Value && off != TimeSpan.Zero;
                if (isPollHere)
                {
                    data["poll"] = "1";
                    data["pollOptions"] = "Пойду|Возможно|Не смогу";
                    data["pollQuestion"] = $"Кто пойдет на «{r.Text}»?";
                }

                var key = $"reminder.event:{r.Id}:{off.TotalSeconds:+#;-#;0}s";
                triggers.Add(TriggerBuilder.Create()
                    .WithIdentity(new TriggerKey(key, "reminder.event"))
                    .ForJob(jobKey)
                    .UsingJobData(data)
                    .StartAt(when.UtcDateTime)
                    .Build());
            }
        }

        if (triggers.Count == 0)
        {
            _log.LogInformation("No triggers for reminder {Id}; nothing scheduled.", r.Id);
            return;
        }

        foreach (var t in triggers)
            await sch.ScheduleJob(t, ct);

        _log.LogInformation("Scheduled {Count} triggers for reminder {Id}", triggers.Count, r.Id);
    }

    public async Task DeleteAndUnschedule(string id, CancellationToken ct = default)
    {
        var sch = await _factory.GetScheduler(ct);
        var tks = await sch.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupEndsWith($":{id}"), ct);
        foreach (var tk in tks) await sch.UnscheduleJob(tk, ct);
    }

    private static bool TryParseLocalOrOffset(string input, TimeZoneInfo tz, out DateTimeOffset utcTime)
    {
        input = input.Trim();
        if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
        {
            if (dto.Offset == TimeSpan.Zero || input.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || input.Contains('+') || input.Contains('-') && input.LastIndexOf('-') > 8)
            {
                utcTime = dto.ToUniversalTime();
            }
            else
            {
                var local = DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Unspecified);
                var zoned = TimeZoneInfo.ConvertTimeToUtc(local, tz);
                utcTime = new DateTimeOffset(zoned, TimeSpan.Zero);
            }
            return true;
        }
        utcTime = default;
        return false;
    }

    private static List<TimeSpan> ParseOffsets(string? input)
    {
        var result = new List<TimeSpan>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var parts = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            try
            {
                var s = p.Trim();

                // ignore 'poll' token if present in offsets string
                if (s.Equals("poll", StringComparison.OrdinalIgnoreCase)) continue;

                var sign = 1.0;
                if (s.StartsWith("+")) s = s[1..];
                else if (s.StartsWith("-")) { sign = -1.0; s = s[1..]; }

                var unit = string.Empty;
                var valueStr = string.Empty;
                foreach (var ch in s)
                {
                    if (char.IsDigit(ch) || ch == '.')
                        valueStr += ch;
                    else
                        unit += ch;
                }

                if (string.IsNullOrEmpty(valueStr)) continue;
                var value = double.Parse(valueStr, CultureInfo.InvariantCulture);
                unit = unit.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(unit)) unit = "m";

                TimeSpan ts = unit switch
                {
                    "s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.FromSeconds(value),
                    "m" or "min" or "mins" or "minute" or "minutes" => TimeSpan.FromMinutes(value),
                    "h" or "hr" or "hour" or "hours" => TimeSpan.FromHours(value),
                    "d" or "day" or "days" => TimeSpan.FromDays(value),
                    _ => TimeSpan.FromMinutes(value)
                };
                result.Add(TimeSpan.FromTicks((long)(ts.Ticks * sign)));
            }
            catch { }
        }
        return result;
    }

    private static bool HasPollFlag(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(p => string.Equals(p, "poll", StringComparison.OrdinalIgnoreCase));
    }

    private static JobDataMap CloneJobData(JobDataMap source)
    {
        var dst = new JobDataMap();
        foreach (var kv in source)
            dst[kv.Key] = kv.Value;
        return dst;
    }
}
