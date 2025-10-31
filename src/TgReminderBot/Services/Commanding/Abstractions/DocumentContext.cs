using System.Threading;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal sealed class DocumentContext
{
    public DocumentContext(Message message, Document document, ITelegramBotClient bot, ILogger logger, System.IServiceProvider services, CancellationToken ct)
    {
        Message = message;
        Document = document;
        Bot = bot;
        Logger = logger;
        Services = services;
        CancellationToken = ct;
    }

    public Message Message { get; }
    public Document Document { get; }
    public ITelegramBotClient Bot { get; }
    public ILogger Logger { get; }
    public System.IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }

    public long ChatId => Message.Chat.Id;
    public int? ThreadId => Message.MessageThreadId;
    public long UserId => Message.From?.Id ?? 0;
}
