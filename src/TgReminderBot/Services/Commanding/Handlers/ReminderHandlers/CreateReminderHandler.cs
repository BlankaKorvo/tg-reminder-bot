using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Data;
using TgReminderBot.Models;
using TgReminderBot.Services;
using TgReminderBot.Services.Commanding;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

[RequireChatAdmin]
[RequireGroup]
[Command("/remind")]
[Description("Создать одноразовое напоминание: /remind <YYYY-MM-DD HH:mm[:ss][Z|+03:00]> — текст")]
internal sealed class CreateReminderHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    private readonly ISchedulingService _sched;

    public CreateReminderHandler(ITelegramBotClient bot, AppDbContext db, ISchedulingService sched)
    {
        _bot = bot;
        _db = db;
        _sched = sched;
    }

    public async Task Execute(CommandContext ctx)
    {
        var (whenRaw, text) = SplitArgs(ctx.Args);

        if (string.IsNullOrWhiteSpace(whenRaw) || string.IsNullOrWhiteSpace(text))
        {
            await _bot.SendMessage(
                ctx.Message.Chat,
                "Формат: /remind <YYYY-MM-DD HH:mm[:ss][Z|+03:00]> — текст\nНапр.: /remind 2025-12-01 21:30 Поздравить",
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: ctx.ThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        // TZ пользователя -> чата -> MSK
        var userTz = await _db.UserSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == ctx.UserId, ctx.CancellationToken);
        var tz = userTz?.TimeZone ?? "Europe/Moscow";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tz);

        // Топик по умолчанию
        int? threadId = null;
        var cs = await _db.ChatSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == ctx.ChatId, ctx.CancellationToken);

        if (cs?.DefaultReminderThreadId is int defThread)
            threadId = defThread;
        else
            threadId = ctx.ThreadId;

        if (text.StartsWith("— ")) text = text[2..];
        if (text.StartsWith("- ")) text = text[2..];

        // Парсим дату/время. Без смещения → локальное время в tz.
        if (!TryParseUserDateTime(whenRaw, zone, out var when))
        {
            await _bot.SendMessage(
                ctx.Message.Chat,
                "Не смог разобрать дату/время. Поддерживается:\n`YYYY-MM-DD HH:mm[:ss][Z|+03:00]` или `HH:mm` (сегодня/завтра в вашей TZ).",
                parseMode: ParseMode.MarkdownV2,
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: threadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var r = new Reminder
        {
            ChatId = ctx.ChatId,
            ThreadId = threadId,
            Text = text,
            ParseMode = null,
            RunAt = when.ToString("O", CultureInfo.InvariantCulture), // ISO-8601 с оффсетом
            Cron = null,
            TimeZone = tz,
            NoPreview = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = ctx.UserId
        };

        _db.Reminders.Add(r);
        await _db.SaveChangesAsync(ctx.CancellationToken);

        await _sched.UpsertAndReschedule(r, tz, "create", ctx.CancellationToken);

        await _bot.SendMessage(
            ctx.Message.Chat,
            $"⏰ Напоминание создано: `{r.Id}`\nВремя: `{when:yyyy-MM-dd HH:mm:ss zzz}` TZ `{tz}`\nТекст: {Escape(text)}",
            parseMode: ParseMode.MarkdownV2,
            replyParameters: ReplyHelper.R(ctx.Message),
            messageThreadId: threadId,
            cancellationToken: ctx.CancellationToken);
    }

    // -------- helpers --------

    // Парсим аргументы: поддержка ISO "YYYY-MM-DD HH:mm[:ss][смещение]" и "HH:mm[:ss]"
    private static (string when, string text) SplitArgs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return ("", "");
        var s = input.Trim();

        // 1) явные разделители " — " / " - "
        var idx = s.IndexOf(" — ", StringComparison.Ordinal);
        if (idx < 0) idx = s.IndexOf(" - ", StringComparison.Ordinal);
        if (idx > 0) return (s[..idx].Trim(), s[(idx + 3)..].Trim());

        // 2) дата+время (пробел или 'T'), опционально со смещением
        var m = Regex.Match(s,
            @"^(?<when>\d{4}-\d{2}-\d{2}[ T]+\d{1,2}:\d{2}(?::\d{2})?(?:\s*(?:Z|[+\-]\d{2}:\d{2}))?)\s+(?<text>.+)$");
        if (m.Success) return (m.Groups["when"].Value, m.Groups["text"].Value);

        // 3) только время
        m = Regex.Match(s, @"^(?<when>\d{1,2}:\d{2}(?::\d{2})?)\s+(?<text>.+)$");
        if (m.Success) return (m.Groups["when"].Value, m.Groups["text"].Value);

        // 4) запасной вариант — первый пробел
        var sp = s.IndexOf(' ');
        if (sp > 0) return (s[..sp].Trim(), s[(sp + 1)..].Trim());
        return (s, "");
    }

    private static bool TryParseUserDateTime(string raw, TimeZoneInfo zone, out DateTimeOffset dto)
    {
        raw = raw.Trim();

        // 1) есть смещение/Z → доверяем системному парсеру
        if (DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var withOffset))
        {
            dto = withOffset;
            return true;
        }

        // 2) "yyyy-MM-dd HH:mm[:ss]" — локальное время в зоне tz
        var fmtsDate = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm" };
        if (DateTime.TryParseExact(raw, fmtsDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dtDate))
        {
            var unspecified = DateTime.SpecifyKind(dtDate, DateTimeKind.Unspecified);
            var offset = zone.GetUtcOffset(unspecified);
            dto = new DateTimeOffset(unspecified, offset);
            return true;
        }

        // 3) "HH:mm[:ss]" — сегодня в зоне, если прошло — переносим на завтра
        var fmtsTime = new[] { "HH:mm:ss", "HH:mm", "H:mm" };
        if (DateTime.TryParseExact(raw, fmtsTime, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dtTime))
        {
            var nowZone = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            var candidate = new DateTime(nowZone.Year, nowZone.Month, nowZone.Day, dtTime.Hour, dtTime.Minute, dtTime.Second);
            var offset = zone.GetUtcOffset(candidate);
            var candidateDto = new DateTimeOffset(candidate, offset);
            if (candidateDto <= nowZone) candidateDto = candidateDto.AddDays(1);
            dto = candidateDto;
            return true;
        }

        dto = default;
        return false;
    }

    private static string Escape(string t)
    {
        // Полный набор для MarkdownV2
        var specials = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
        foreach (var s in specials) t = t.Replace(s, "\\" + s);
        return t;
    }
}