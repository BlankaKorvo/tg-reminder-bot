using System.Threading.Tasks;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal interface ICommandHandler
{
    Task Execute(CommandContext ctx);
}
