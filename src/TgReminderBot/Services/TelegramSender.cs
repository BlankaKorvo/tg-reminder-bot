using Polly;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgReminderBot.Services;

public interface ITelegramSender
{
    Task SendText(long chatId, string text, ParseMode? mode, int? threadId, bool noPreview, CancellationToken ct);
}

public sealed class TelegramSender : ITelegramSender
{
    private readonly ITelegramBotClient _bot;

    public TelegramSender(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task SendText(long chatId, string text, ParseMode? mode, int? threadId, bool noPreview, CancellationToken ct)
    {
        var chunks = ChunkText(text, 4000);

        var retry = Policy
            .Handle<ApiRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(2 * i));

        foreach (var payload in chunks)
        {
            await retry.ExecuteAsync(async token =>
            {
                LinkPreviewOptions? preview = noPreview ? new LinkPreviewOptions { IsDisabled = true } : null;

                if (mode.HasValue)
                {
                    await _bot.SendMessage(
                        chatId: chatId,
                        text: payload,
                        parseMode: mode.Value,              
                        messageThreadId: threadId,
                        linkPreviewOptions: preview,
                        cancellationToken: token
                    );
                }
                else
                {
                    await _bot.SendMessage(
                        chatId: chatId,
                        text: payload,
                        messageThreadId: threadId,
                        linkPreviewOptions: preview,
                        cancellationToken: token
                    );
                }
            }, ct);
        }
    }

    private static IEnumerable<string> ChunkText(string input, int max)
    {
        if (string.IsNullOrEmpty(input)) yield break;
        for (int i = 0; i < input.Length; i += max)
            yield return input.Substring(i, Math.Min(max, input.Length - i));
    }
}