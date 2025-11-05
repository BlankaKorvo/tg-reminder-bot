using System;
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
[Command("/rmallmine")]
[Description("Удалить все мои напоминания в текущем чате")]
internal sealed class DeleteAllMineHandler : ICommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    private readonly ISchedulingService _sched;

    public DeleteAllMineHandler(ITelegramBotClient bot, AppDbContext db, ISchedulingService sched)
    {
        _bot = bot;
        _db = db;
        _sched = sched;
    }

    public async Task Execute(CommandContext ctx)
    {
        var list = await _db.Reminders
            .Where(x => x.ChatId == ctx.ChatId && x.CreatedBy == ctx.UserId)
            .Select(x => x.Id)
            .ToListAsync(ctx.CancellationToken);

        if (list.Count == 0)
        {
            await _bot.SendMessage(ctx.Message.Chat, "У тебя нет напоминаний в этом чате.",
                replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
            return;
        }

        var entities = await _db.Reminders.Where(x => list.Contains(x.Id)).ToListAsync(ctx.CancellationToken);
        _db.Reminders.RemoveRange(entities);
        await _db.SaveChangesAsync(ctx.CancellationToken);

        foreach (var id in list)
        {
            try { await _sched.DeleteAndUnschedule(id, ctx.CancellationToken); } catch { /* ignore */ }
        }

        await _bot.SendMessage(ctx.Message.Chat, $"Удалено моих напоминаний: {list.Count}",
            replyParameters: ReplyHelper.R(ctx.Message), messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
    }
}
