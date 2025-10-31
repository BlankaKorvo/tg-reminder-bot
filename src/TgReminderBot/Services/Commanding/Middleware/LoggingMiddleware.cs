using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Middleware;

internal sealed class LoggingMiddleware : ICommandMiddleware
{
    public Task InvokeAsync(CommandContext ctx, Func<Task> next)
    {
        ctx.Logger.LogInformation("Cmd {Cmd} chat={Chat} user={User} topic={Topic}", 
            ctx.Message.Text?.Split(' ', 2)[0], ctx.ChatId, ctx.UserId, ctx.ThreadId);
        return next();
    }
}
