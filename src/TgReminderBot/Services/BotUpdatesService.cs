using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Services.Commanding;

namespace TgReminderBot.Services;

public class BotUpdatesService : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<BotUpdatesService> _log;
    private readonly IServiceProvider _sp;
    private readonly BotCommandScopesPublisher _publisher;

    public BotUpdatesService(ITelegramBotClient bot, ILogger<BotUpdatesService> log, IServiceProvider sp, BotCommandScopesPublisher publisher) {
        _bot = bot;
        _log = log;
        _sp = sp;
        _publisher = publisher;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        _bot.StartReceiving(HandleUpdate, HandleError, receiverOptions, stoppingToken);
        _log.LogInformation("Bot receiving started.");
        return Task.CompletedTask;
    }

    private async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            if (update.Message is { } m)
            {
                _log.LogInformation("Msg: type={Type} chat={ChatId} from={UserId} text={Text}",
                    m.Type, m.Chat.Id, m.From?.Id, m.Text);

                if (m.Chat.Type == ChatType.Group || m.Chat.Type == ChatType.Supergroup)
                    await _publisher.EnsureChatPublishedAsync(m.Chat.Id, ct);

                if (m.Type == MessageType.Text && !string.IsNullOrWhiteSpace(m.Text))
                    await CommandRouter.Dispatch(m, m.Text!, _sp, ct);
                else if (m.Type == MessageType.Document && m.Document is { } doc)
                    await CommandRouter.DispatchDocument(m, doc, _sp, ct);

                return;
            }

            if (update.CallbackQuery is { } cq)
            {
                _log.LogInformation("Callback: chat={ChatId} from={UserId} data={Data}",
                    cq.Message?.Chat.Id, cq.From.Id, cq.Data);
                await CommandRouter.DispatchCallback(cq, _sp, ct);
                return;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Update handling failed");
        }
    }

    private Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        _log.LogError(ex, "Telegram polling error");
        return Task.CompletedTask;
    }
}
