using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Services;
using TgReminderBot.Services.Commanding;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

[RequireGroup]
[Command("/rmrems")]
[Description("Удалить несколько напоминаний по id или префиксам: /rmrems <id|prefix> [id2|prefix2] ... (поддерживает переносы строк, запятые и ;)")]
internal sealed class BulkDeleteRemindersHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    private readonly ISchedulingService _sched;

    public BulkDeleteRemindersHandler(ITelegramBotClient bot, AppDbContext db, ISchedulingService sched)
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
                "Использование: /rmrems <id|prefix> [id2|prefix2] ...\\nИдентификаторы можно писать на новых строках, через пробел/запятую/точку с запятой.",
                replyParameters: ReplyHelper.R(ctx.Message),
                messageThreadId: ctx.ThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var tokens = raw.Replace("\r", string.Empty)
                        .Split(new[] { '\n', ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(200)
                        .ToList();

        if (tokens.Count == 0)
        {
            await _bot.SendMessage(ctx.Message.Chat, "Не нашёл ни одного ключа для удаления.",
                replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
            return;
        }

        var toRemove = new List<string>();
        var notFound = new List<string>();

        foreach (var key in tokens)
        {
            var r = await _db.Reminders
                .Where(x => x.ChatId == ctx.ChatId && x.CreatedBy == ctx.UserId
                            && (x.Id == key || x.Id.StartsWith(key)))
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ctx.CancellationToken);

            if (r is null) { notFound.Add(key); continue; }
            toRemove.Add(r.Id);
        }

        if (toRemove.Count == 0)
        {
            await _bot.SendMessage(ctx.Message.Chat, "Ничего не удалено. " + (notFound.Count > 0 ? $"Не найдено: {string.Join(", ", notFound.Take(10))}" : ""),
                replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
            return;
        }

        var entities = await _db.Reminders.Where(x => toRemove.Contains(x.Id)).ToListAsync(ctx.CancellationToken);
        _db.Reminders.RemoveRange(entities);
        await _db.SaveChangesAsync(ctx.CancellationToken);

        foreach (var id in toRemove)
        {
            try { await _sched.DeleteAndUnschedule(id, ctx.CancellationToken); } catch { /* ignore */ }
        }

        var msg = $"Удалено: {toRemove.Count}";
        if (notFound.Count > 0) msg += $"; не найдено: {notFound.Count} ({string.Join(", ", notFound.Take(10))})";

        await _bot.SendMessage(ctx.Message.Chat, msg,
            replyParameters: ReplyHelper.R(ctx.Message),
            messageThreadId: ctx.ThreadId,
            cancellationToken: ctx.CancellationToken);
    }
}
