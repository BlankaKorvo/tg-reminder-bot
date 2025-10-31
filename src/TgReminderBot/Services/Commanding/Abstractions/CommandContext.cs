using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Microsoft.Extensions.Logging;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal sealed class CommandContext
{
    public CommandContext(Message message, string args, ITelegramBotClient bot, ILogger logger, System.IServiceProvider services, CancellationToken ct)
    {
        Message = message;
        Args = args;
        Bot = bot;
        Logger = logger;
        Services = services;
        CancellationToken = ct;
    }

    public Message Message { get; }
    public string Args { get; }
    public ITelegramBotClient Bot { get; }
    public ILogger Logger { get; }
    public System.IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }

    public long ChatId => Message.Chat.Id;
    public int? ThreadId => Message.MessageThreadId;
    public long UserId => Message.From?.Id ?? 0;
}
