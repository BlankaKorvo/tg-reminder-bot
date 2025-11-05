
using Polly;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgReminderBot.Services;

public interface ITelegramSender
{
    Task SendText(long chatId, string text, ParseMode? mode, int? threadId, bool noPreview, CancellationToken ct);
    Task SendPoll(long chatId, string question, IReadOnlyList<string> options, int? threadId, CancellationToken ct, bool isAnonymous = false, bool allowsMultipleAnswers = false);
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
        // Telegram hard limit ~4096 chars per message with markdown; режем на куски
        var chunks = ChunkText(text, 3500).ToArray();
        foreach (var chunk in chunks)
        {
            await Policy
                .Handle<ApiRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                })
                .ExecuteAsync(async token =>
                {
                    LinkPreviewOptions? preview = noPreview ? new LinkPreviewOptions { IsDisabled = true } : null;

                    if (mode.HasValue)
                    {
                        // Библиотеки, где parseMode не-nullable: передаём .Value
                        await _bot.SendMessage(
                            chatId,
                            chunk,
                            mode.Value,
                            messageThreadId: threadId,
                            linkPreviewOptions: preview,
                            cancellationToken: token
                        );
                    }
                    else
                    {
                        // Без parseMode — используем перегрузку/опциональные параметры
                        await _bot.SendMessage(
                            chatId,
                            chunk,
                            messageThreadId: threadId,
                            linkPreviewOptions: preview,
                            cancellationToken: token
                        );
                    }
                }, ct);
        }
    }

    public async Task SendPoll(long chatId, string question, IReadOnlyList<string> options, int? threadId, CancellationToken ct, bool isAnonymous = false, bool allowsMultipleAnswers = false)
    {
        var opts = options.Where(s => !string.IsNullOrWhiteSpace(s))
                          .Select(s => s.Trim())
                          .Take(10)
                          .Select(s => new InputPollOption(s))
                          .ToList();

        if (opts.Count < 2) opts = new List<InputPollOption> { new("Да"), new("Нет") };

        await Policy
            .Handle<ApiRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
            })
            .ExecuteAsync(async token =>
            {
                await _bot.SendPoll(
                    chatId,
                    question,
                    opts,
                    isAnonymous: isAnonymous,
                    allowsMultipleAnswers: allowsMultipleAnswers,
                    messageThreadId: threadId,
                    cancellationToken: token
                );
            }, ct);
    }

    private static IEnumerable<string> ChunkText(string input, int max)
    {
        if (string.IsNullOrEmpty(input)) yield break;
        for (int i = 0; i < input.Length; i += max)
            yield return input.Substring(i, Math.Min(max, input.Length - i));
    }
}
