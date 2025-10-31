using System;
using System.Threading.Tasks;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Middleware;

// Placeholder. Extend to keep per-user/chat rate limits.
internal sealed class ThrottlingMiddleware : ICommandMiddleware
{
    public Task InvokeAsync(CommandContext ctx, Func<Task> next) => next();
}
