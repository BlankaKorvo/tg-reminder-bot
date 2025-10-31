using System;
using System.Threading.Tasks;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal interface ICommandMiddleware
{
    Task InvokeAsync(CommandContext ctx, Func<Task> next);
}
