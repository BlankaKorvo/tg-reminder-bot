//using System.ComponentModel;
//using System.Threading.Tasks;
//using Telegram.Bot;
//using TgReminderBot.Data;
//using TgReminderBot.Models;
//using TgReminderBot.Services.Commanding.Abstractions;
//using TgReminderBot.Services.Commanding.Abstractions.Attributes;

//namespace TgReminderBot.Services.Commanding.Handlers.ReminderHandlers;

//[RequireGroup]
//[RequireChatAdmin]
//[Command("/setcontroltopic")]
//[Description("Set the default topic for reminders in this chat.")]
//internal sealed class SetControlTopicHandler : ICommandHandler
//{
//    private readonly ITelegramBotClient _bot;
//    private readonly AppDbContext _db;
//    public SetControlTopicHandler(ITelegramBotClient bot, AppDbContext db) { _bot = bot; _db = db; }

//    public async Task Execute(CommandContext ctx)
//    {
//        var id = ctx.ThreadId;
//        if (id is null)
//        {
//            await _bot.SendMessage(ctx.Message.Chat, "Run this inside a topic",
//                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
//                cancellationToken: ctx.CancellationToken);
//            return;
//        }

//        var existing = await _db.ChatSettings.FindAsync(new object?[] { ctx.ChatId }, ctx.CancellationToken);
//        if (existing is null)
//        {
//            var cs = new ChatSettings { ChatId = ctx.ChatId, ControlThreadId = id };
//            await _db.ChatSettings.AddAsync(cs, ctx.CancellationToken);
//        }
//        else
//        {
//            existing.ControlThreadId = id;
//            _db.ChatSettings.Update(existing);
//        }

//        await _db.SaveChangesAsync(ctx.CancellationToken);
//        await _bot.SendMessage(ctx.Message.Chat, $"Control topic set: {id}",
//            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
//            messageThreadId: id, cancellationToken: ctx.CancellationToken);
//    }
//}
