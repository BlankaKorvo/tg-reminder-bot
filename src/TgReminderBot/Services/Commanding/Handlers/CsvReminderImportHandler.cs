using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Handlers;

// Minimal CSV import handler placeholder. Matches *.csv documents.
internal sealed class CsvReminderImportHandler : IDocumentHandler
{
    public bool CanHandle(Document doc) => (doc.FileName?.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase) ?? false);

    public async Task Execute(DocumentContext ctx)
    {
        await ctx.Bot.SendMessage(ctx.Message.Chat, "CSV import is not implemented in this build.",
            replyParameters: new ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
