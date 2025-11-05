using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Models;
using TgReminderBot.Services;
using TgReminderBot.Services.Commanding;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

[RequireGroup]
[Command("/event")]
[Description("Создать событие с напоминаниями заранее: /event <YYYY-MM-DD HH:mm[:ss][Z|+03:00]> <offsets> — текст. Примеры: /event 2025-11-07 19:00 -1d,-4h — Репетиция")]
internal sealed class EventReminderHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    private readonly ISchedulingService _sched;

    public EventReminderHandler(ITelegramBotClient bot, AppDbContext db, ISchedulingService sched)
    {
        _bot = bot;
        _db = db;
        _sched = sched;
    }

    public async Task Execute(CommandContext ctx)
    {
        var raw = (ctx.Args ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            await _bot.SendMessage(ctx.Message.Chat,
                "Использование: /event <YYYY-MM-DD HH:mm[:ss][Z|+03:00]> <offsets> — текст\nНапр.: /event 2025-11-07 19:00 -1d,-4h — Репетиция",
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: ctx.ThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        // Разделяем на левую часть (время + offsets) и текст после тире
        var parts = raw.Split('—', 2);
        if (parts.Length < 2)
        {
            await _bot.SendMessage(ctx.Message.Chat, "Нужен текст после — (длинное тире).",
                replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
            return;
        }
        var left = parts[0].Trim();
        var text = parts[1].Trim();
        if (string.IsNullOrEmpty(text))
        {
            await _bot.SendMessage(ctx.Message.Chat, "Пустой текст напоминания.",
                replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
            return;
        }

        // Разбираем дату/время и список оффсетов (например: -1d,-4h или offsets=-1d;0;+5m)
        // Дата/время - первый токен(ы) до пробела, допускаем формат с секундой и с Z/смещением
        var m = Regex.Match(left, @"^(?<dt>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}(?::\d{2})?(?:\s*(?:Z|[+\-]\d{2}:\d{2}))?)\s*(?<offs>.*)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            await _bot.SendMessage(ctx.Message.Chat, "Не удалось разобрать дату/время. Ожидается: YYYY-MM-DD HH:mm[:ss][Z|+03:00]",
                replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
            return;
        }

        var dtStr = m.Groups["dt"].Value.Trim();
        var offsStr = m.Groups["offs"].Value.Trim();
        offsStr = NormalizeOffsets(offsStr);

        // Tz пользователя
        var userTz = await _db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == ctx.UserId, ctx.CancellationToken);
        var tz = userTz?.TimeZone ?? "Europe/Moscow";

        // Топик по умолчанию
        int? threadId = ctx.ThreadId;
        var cs = await _db.ChatSettings.AsNoTracking().FirstOrDefaultAsync(x => x.ChatId == ctx.ChatId, ctx.CancellationToken);
        if (threadId is null && cs?.DefaultReminderThreadId is int defThread)
            threadId = defThread;

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
            RemindOffsets = offsStr,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = ctx.UserId
        };

        _db.Reminders.Add(r);
        await _db.SaveChangesAsync(ctx.CancellationToken);
        await _sched.UpsertAndReschedule(r, tz, "event", ctx.CancellationToken);

        await _bot.SendMessage(ctx.Message.Chat, $"Событие создано: `{r.Id}`\nНапоминаний: {(string.IsNullOrEmpty(offsStr) ? 1 : offsStr.Split(new[]{',',';',' '}, StringSplitOptions.RemoveEmptyEntries).Length)}",
            replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
    }

    private static string NormalizeOffsets(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "0";
        s = s.Trim();
        // Если пользователь написал offsets=...
        s = Regex.Replace(s, "^offsets\\s*=\\s*", "", RegexOptions.IgnoreCase).Trim();
        // Удаляем лишние слова, оставляем только токены вида (+|-)N[smhd]
        s = s.Replace("и", " ").Replace("за", " ").Replace("через", " ");
        // Разделители -> запятая
        s = Regex.Replace(s, "[;\\s]+", ",");
        // Убираем пустые
        s = string.Join(",", s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return s;
    }
}
