using System;
using System.Threading.Tasks;
using Telegram.Bot;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Middleware;

internal sealed class AuthorizationMiddleware : ICommandMiddleware
{
    private readonly IAccessGuard _guard;
    public AuthorizationMiddleware(IAccessGuard guard) => _guard = guard;

    public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
    {
        var result = await _guard.Authorize(ctx.Message, ctx.CancellationToken);
        if (!result.Allowed)
        {
            await ctx.Bot.SendMessage(ctx.Message.Chat, "Access denied",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }
        await next();
    }
}
