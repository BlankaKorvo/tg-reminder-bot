
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

[RequireChatAdmin]
[RequireGroup]
[Command("/events")]
[Description("Пакетное создание событий. Формат: <YYYY-MM-DD HH:mm[:ss][Z|+03:00]> <offsets> — <текст>. Опция: слово 'poll' в offsets создаст опрос на последнем напоминании (кроме 0).")]
internal sealed class BulkEventsHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    private readonly ISchedulingService _sched;

    public BulkEventsHandler(ITelegramBotClient bot, AppDbContext db, ISchedulingService sched)
    {
        _bot = bot;
        _db = db;
        _sched = sched;
    }

    public async Task Execute(CommandContext ctx)
    {
        var body = (ctx.Args ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            await _bot.SendMessage(ctx.Message.Chat,
                "Использование: /events и далее в сообщении по одной строке на событие:\n" +
                "YYYY-MM-DD HH:mm[:ss][Z|+03:00] <offsets> — <текст>\n" +
                "Offsets: -1d,-4h,-15m,0,+5m ... Разделители: пробел/запятая/;.\n" +
                "Опционально: добавь слово 'poll' среди offsets, чтобы последнее напоминание (кроме 0) было опросом.",
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: ctx.ThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var tz = (await _db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == ctx.UserId, ctx.CancellationToken))?.TimeZone ?? "Europe/Moscow";

        int? threadId = null;
        var cs = await _db.ChatSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == ctx.ChatId, ctx.CancellationToken);

        if (cs?.DefaultReminderThreadId is int defThread)
            threadId = defThread;
        else
            threadId = ctx.ThreadId;

        var lines = body.Replace("\r", string.Empty)
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Take(200)
                        .ToList();

        var created = new List<Reminder>();
        var errors = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            var parts = line.Split('—', 2);
            if (parts.Length < 2)
            {
                var parts2 = Regex.Split(line, "\\s-\\s", RegexOptions.CultureInvariant);
                if (parts2.Length >= 2) parts = new[] { parts2[0], string.Join(" - ", parts2.Skip(1)) };
            }
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                errors.Add($"⛔ Нет текста: '{line}'");
                continue;
            }

            var left = parts[0].Trim();
            var text = parts[1].Trim();

            var m = Regex.Match(left, @"^(?<dt>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}(?::\d{2})?(?:\s*(?:Z|[+\-]\d{2}:\d{2}))?)\s*(?<offs>.*)$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                errors.Add($"⛔ Дата/время не распознаны: '{line}'");
                continue;
            }

            var dtStr = m.Groups["dt"].Value.Trim();
            var offsRaw = m.Groups["offs"].Value;

            var (offs, poll) = NormalizeOffsetsAndPoll(offsRaw);

            var r = new Reminder
            {
                ChatId = ctx.ChatId,
                ThreadId = threadId,
                Text = text,
                ParseMode = null,
                RunAt = null,
                Cron = null,
                TimeZone = tz,
                NoPreview = true,
                EventAt = dtStr,
                RemindOffsets = offs,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = ctx.UserId
            };
            created.Add(r);

            // маркер для планировщика через tag (см. ниже)
            r.ParseMode = poll ? "poll" : null;
        }

        if (created.Count == 0)
        {
            await _bot.SendMessage(ctx.Message.Chat,
                "Ни одной валидной строки.\n" + string.Join("\n", errors.Take(5)),
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: ctx.ThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        await _db.Reminders.AddRangeAsync(created, ctx.CancellationToken);
        await _db.SaveChangesAsync(ctx.CancellationToken);

        int ok = 0;
        foreach (var r in created)
        {
            try
            {
                var tag = "bulk" + (string.Equals(r.ParseMode, "poll", StringComparison.OrdinalIgnoreCase) ? "+poll" : "");
                await _sched.UpsertAndReschedule(r, tz, tag, ctx.CancellationToken);
                ok++;
            }
            catch (Exception ex) { errors.Add($"⚠️ {r.Id}: {ex.Message}"); }
        }

        var reply = $"Создано: {created.Count}. Запланировано: {ok}.";
        if (errors.Count > 0) reply += "\n" + string.Join("\n", errors.Take(10));

        await _bot.SendMessage(ctx.Message.Chat, reply,
            replyParameters: ReplyHelper.R(ctx.Message),
            messageThreadId: ctx.ThreadId,
            cancellationToken: ctx.CancellationToken);
    }

    private static (string Offsets, bool Poll) NormalizeOffsetsAndPoll(string s)
    {
        bool poll = false;
        if (string.IsNullOrWhiteSpace(s)) return ("0", false);
        s = s.Trim();
        s = Regex.Replace(s, "^offsets\\s*=\\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
        s = s.Replace("и", " ").Replace("за", " ").Replace("через", " ");

        var tokens = Regex.Split(s, @"[;\s,]+", RegexOptions.CultureInvariant)
            .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();

        var clean = new List<string>();
        foreach (var t in tokens)
        {
            if (string.Equals(t, "poll", StringComparison.OrdinalIgnoreCase)) { poll = true; continue; }
            clean.Add(t);
        }
        var offs = string.Join(",", clean);
        if (string.IsNullOrWhiteSpace(offs)) offs = "0";
        return (offs, poll);
    }
}
