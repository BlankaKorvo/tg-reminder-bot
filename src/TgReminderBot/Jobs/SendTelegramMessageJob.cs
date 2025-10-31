using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Services;

namespace TgReminderBot.Jobs;

public class SendTelegramMessageJob : IJob
{
    private readonly ITelegramSender _sender;
    private readonly ILogger<SendTelegramMessageJob> _log;

    public SendTelegramMessageJob(ITelegramSender sender, ILogger<SendTelegramMessageJob> log)
    {
        _sender = sender;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var map = context.MergedJobDataMap;

        long chatId = map.GetLong("chatId");
        int? threadId = map.ContainsKey("threadId") ? (map["threadId"] as int? ?? (map.GetInt("threadId"))) : null;
        var text = map.GetString("text") ?? string.Empty;
        var parseModeString = map.GetString("parseMode");
        bool noPreview = map.TryGetValue("noPreview", out var np) && Convert.ToBoolean(np);

        ParseMode? parseMode = parseModeString?.Trim().ToUpperInvariant() switch
        {
            "HTML" => ParseMode.Html,
            "MARKDOWNV2" => ParseMode.MarkdownV2,
            "MARKDOWN" => ParseMode.Markdown,
            _ => null
        };

        try
        {
            await _sender.SendText(chatId, text, parseMode, threadId, noPreview, context.CancellationToken);
            _log.LogInformation("Sent reminder to {ChatId}{Thread}.", chatId, threadId is null ? "" : $" thread {threadId}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Telegram send failed for chat {ChatId}, thread {ThreadId}", chatId, threadId);
            throw;
        }
    }
}