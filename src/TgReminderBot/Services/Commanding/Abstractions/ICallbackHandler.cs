using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal interface ICallbackHandler
{
    bool CanHandle(CallbackQuery cq);
    Task Execute(CallbackContext ctx);
}
