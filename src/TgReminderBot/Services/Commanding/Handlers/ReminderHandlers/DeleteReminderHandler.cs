//using System;
//using System.ComponentModel;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using Telegram.Bot;
//using TgReminderBot.Data;
//using TgReminderBot.Services.Commanding.Abstractions;
//using TgReminderBot.Services.Commanding.Abstractions.Attributes;

//namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

//[RequireGroup]
//[Command("/rmrem")]
//[Description("Удалить напоминание по id: /rmrem <id|id_prefix>")]
//internal sealed class DeleteReminderHandler : ICommandHandler
//{
//    private readonly ITelegramBotClient _bot;
//    private readonly AppDbContext _db;
//    private readonly ISchedulingService _sched;

//    public DeleteReminderHandler(ITelegramBotClient bot, AppDbContext db, ISchedulingService sched)
//    {
//        _bot = bot;
//        _db = db;
//        _sched = sched;
//    }

//    public async Task Execute(CommandContext ctx)
//    {
//        var key = ctx.Args?.Trim() ?? "";
//        if (string.IsNullOrWhiteSpace(key))
//        {
//            await _bot.SendMessage(ctx.Message.Chat, "Формат: /rmrem <id|prefix>",
//                replyParameters: ReplyHelper.R(ctx.Message),
//                messageThreadId: ctx.ThreadId,
//                cancellationToken: ctx.CancellationToken);
//            return;
//        }

//        var r = await _db.Reminders.FirstOrDefaultAsync(x =>
//                x.ChatId == ctx.ChatId && x.CreatedBy == ctx.UserId &&
//                (x.Id == key || x.Id.StartsWith(key)),
//            ctx.CancellationToken);

//        if (r is null)
//        {
//            await _bot.SendMessage(ctx.Message.Chat, "Не найдено.",
//                replyParameters: ReplyHelper.R(ctx.Message),
//                messageThreadId: ctx.ThreadId,
//                cancellationToken: ctx.CancellationToken);
//            return;
//        }

//        _db.Reminders.Remove(r);
//        await _db.SaveChangesAsync(ctx.CancellationToken);
//        await _sched.DeleteAndUnschedule(r.Id, ctx.CancellationToken);

//        await _bot.SendMessage(ctx.Message.Chat, $"Удалено `{r.Id}`",
//            replyParameters: ReplyHelper.R(ctx.Message),
//            messageThreadId: ctx.ThreadId,
//            cancellationToken: ctx.CancellationToken);
//    }
//}