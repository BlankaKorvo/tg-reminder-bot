using System.Threading;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgReminderBot.Services.Commanding.Abstractions;

internal sealed class CallbackContext
{
    public CallbackContext(CallbackQuery cq, ITelegramBotClient bot, ILogger logger, System.IServiceProvider services, CancellationToken ct)
    {
        Callback = cq;
        Bot = bot;
        Logger = logger;
        Services = services;
        CancellationToken = ct;
    }

    public CallbackQuery Callback { get; }
    public ITelegramBotClient Bot { get; }
    public ILogger Logger { get; }
    public System.IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }

    public Message? Message => Callback.Message;
    public long ChatId => Callback.Message?.Chat.Id ?? 0;
    public int? ThreadId => Callback.Message?.MessageThreadId;
    public long UserId => Callback.From?.Id ?? 0;
    public string? Data => Callback.Data;
}
