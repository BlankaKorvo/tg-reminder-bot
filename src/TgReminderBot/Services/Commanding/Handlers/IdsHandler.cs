//using System.ComponentModel;
//using System.Threading.Tasks;
//using Telegram.Bot;
//using Telegram.Bot.Types.Enums;
//using TgReminderBot.Common;
//using TgReminderBot.Services.Commanding.Abstractions;
//using TgReminderBot.Services.Commanding.Abstractions.Attributes;

//namespace TgReminderBot.Services.Commanding.Handlers;

//[Command("/ids")]
//[Description("Show chat and thread IDs.")]
//[RequireAll]
//internal sealed class IdsHandler : ICommandHandler
//{
//    private readonly ITelegramBotClient _bot;
//    public IdsHandler(ITelegramBotClient bot) => _bot = bot;

//    public async Task Execute(CommandContext ctx)
//    {
//        var s = $"chat_id: `{ctx.ChatId}`\nthread_id: `{ctx.ThreadId?.ToString() ?? "null"}`";
//        await _bot.SendMessage(ctx.Message.Chat, s.ToMd2(), parseMode: ParseMode.MarkdownV2,
//            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
//            messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
//    }
//}
