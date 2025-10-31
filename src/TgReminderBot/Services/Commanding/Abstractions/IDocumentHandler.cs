using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal interface IDocumentHandler
{
    bool CanHandle(Document doc);
    Task Execute(DocumentContext ctx);
}
