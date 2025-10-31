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

        // Clean up all reminder triggers
        var all = await sch.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupStartsWith("reminder"), ct);
        foreach (var tk in all)
            await sch.UnscheduleJob(tk, ct);

        // Load active reminders
        var reminders = await db.Reminders.AsNoTracking().ToListAsync(ct);
        foreach (var r in reminders)
        {
            try
            {
                await UpsertAndReschedule(r, defaultTz, null, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to reschedule reminder {Id}", r.Id);
            }
        }
    }

    public async Task UpsertAndReschedule(Reminder r, string defaultTz, string? tag = null, CancellationToken ct = default)
    {
        var sch = await _factory.GetScheduler(ct);

        // Remove previous triggers for this reminder
        var existing = await sch.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupEndsWith($":{r.Id}"), ct);
        foreach (var tk in existing)
            await sch.UnscheduleJob(tk, ct);

        // Normalize timezone
        var tzId = string.IsNullOrWhiteSpace(r.TimeZone) ? defaultTz : r.TimeZone.Trim();
        TimeZoneInfo tz;
        try { tz = TZConvert.GetTimeZoneInfo(tzId); }
        catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }

        // Build job key
        var jobKey = new JobKey($"reminder:{r.Id}", "reminders");
        if (!await sch.CheckExists(jobKey, ct))
        {
            await sch.AddJob(JobBuilder.Create<SendTelegramMessageJob>()
                .WithIdentity(jobKey)
                .StoreDurably()
                .Build(), true, ct);
        }

        // Common job data
        var data = new JobDataMap
        {
            ["chatId"] = r.ChatId,
            ["text"] = r.Text ?? string.Empty,
            ["parseMode"] = r.ParseMode ?? string.Empty,
            ["noPreview"] = r.NoPreview,
            ["_tag"] = tag ?? string.Empty
        };
        if (r.ThreadId is int threadId) // только если есть
            data["threadId"] = threadId;

        var triggers = new List<ITrigger>();

        // 1) One-time RunAt (in local tz if no suffix)
        if (!string.IsNullOrWhiteSpace(r.RunAt) && TryParseLocalOrOffset(r.RunAt!, tz, out var runAtUtc))
        {
            if (runAtUtc > DateTimeOffset.UtcNow)
            {
                triggers.Add(TriggerBuilder.Create()
                    .WithIdentity(new TriggerKey($"runat:{r.Id}", "reminder.single"))
                    .ForJob(jobKey)
                    .StartAt(runAtUtc.UtcDateTime)
                    .UsingJobData(data)
                    .Build());
            }
        }

        // 2) Cron (no offsets supported for cron)
        if (!string.IsNullOrWhiteSpace(r.Cron))
        {
            try
            {
                var cron = r.Cron!.Trim();
                triggers.Add(TriggerBuilder.Create()
                    .WithIdentity(new TriggerKey($"cron:{r.Id}", "reminder.cron"))
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

        // 3) EventAt + RemindOffsets (comma/space/semicolon separated, e.g. "-15m; 0; +5m; -1h")
        if (!string.IsNullOrWhiteSpace(r.EventAt) && TryParseLocalOrOffset(r.EventAt!, tz, out var eventUtc))
        {
            var offsets = ParseOffsets(r.RemindOffsets);
            if (offsets.Count == 0) offsets.Add(TimeSpan.Zero);

            foreach (var off in offsets)
            {
                var when = eventUtc + off;
                if (when <= DateTimeOffset.UtcNow) continue;

                var key = $"event:{r.Id}:{off.TotalSeconds:+#;-#;0}s";
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

        var tks = await sch.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupStartsWith("reminder"), ct);
        foreach (var tk in tks)
        {
            if (tk.Name.Contains(id, StringComparison.Ordinal))
                await sch.UnscheduleJob(tk, ct);
        }

        var jobKey = new JobKey($"reminder:{id}", "reminders");
        if (await sch.CheckExists(jobKey, ct))
            await sch.DeleteJob(jobKey, ct);

        _log.LogInformation("Unschedule complete for reminder {Id}", id);
    }

    private static bool TryParseLocalOrOffset(string input, TimeZoneInfo tz, out DateTimeOffset utc)
    {
        // Accept ISO-8601 with or without offset; if no offset -> interpret as local in tz
        if (DateTimeOffset.TryParse(input, out var dto))
        {
            if (input.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || input.Contains('+'))
            {
                utc = dto.ToUniversalTime();
                return true;
            }
            var local = DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Unspecified);
            var mapped = TimeZoneInfo.ConvertTimeToUtc(local, tz);
            utc = new DateTimeOffset(mapped, TimeSpan.Zero);
            return true;
        }
        utc = default;
        return false;
    }

    private static List<TimeSpan> ParseOffsets(string? raw)
    {
        var result = new List<TimeSpan>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var parts = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var s = p.Trim();
            try
            {
                var sign = 1;
                if (s.StartsWith("+")) { sign = 1; s = s[1..]; }
                else if (s.StartsWith("-")) { sign = -1; s = s[1..]; }

                double value = 0;
                string unit = "m";
                for (int i = 0; i < s.Length; i++)
                {
                    if (!char.IsDigit(s[i]) && s[i] != '.')
                    {
                        value = double.Parse(s[..i]);
                        unit = s[i..].ToLowerInvariant();
                        goto Parsed;
                    }
                }
                value = double.Parse(s);
            Parsed:
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
}