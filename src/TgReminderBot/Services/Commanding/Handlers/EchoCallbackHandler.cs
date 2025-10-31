using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Handlers;

internal sealed class EchoCallbackHandler : ICallbackHandler
{
    public bool CanHandle(CallbackQuery cq) => cq.Data != null && cq.Data.StartsWith("echo:");

    public async Task Execute(CallbackContext ctx)
    {
        if (ctx.Message is { } msg)
            await ctx.Bot.SendMessage(msg.Chat, $"Callback: {ctx.Data}",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = msg.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
    }
}
