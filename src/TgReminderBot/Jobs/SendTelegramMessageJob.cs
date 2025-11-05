
using System.Globalization;
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

        if (!map.TryGetString("chatId", out var chatStr) || !long.TryParse(chatStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
            return;

        string text = map.TryGetString("text", out var t) ? t! : string.Empty;
        string? parse = map.TryGetString("parseMode", out var pm) ? pm : null;
        var parseMode = string.IsNullOrWhiteSpace(parse) ? (ParseMode?)null : Enum.TryParse<ParseMode>(parse, out var pmode) ? pmode : null;
        bool noPreview = map.TryGetString("noPreview", out var np) && (np == "1" || np.Equals("true", StringComparison.OrdinalIgnoreCase));
        int? threadId = null;
        if (map.TryGetString("threadId", out var tidStr) && int.TryParse(tidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tid))
            threadId = tid;

        // Optional: humanized time-left from per-trigger offset (seconds)
        if (map.TryGetString("timeLeftSec", out var tls) && long.TryParse(tls, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sec))
        {
            var human = HumanizeDuration(TimeSpan.FromSeconds(sec));
            if (sec <= 1)
                text = $"{text} — начинается сейчас";
            else
                text = $"{text} — произойдёт через {human}";
        }

        // If poll flag set — send poll instead of text
        bool sendPoll = map.TryGetString("poll", out var pollFlag) && (pollFlag == "1" || pollFlag.Equals("true", StringComparison.OrdinalIgnoreCase));
        if (sendPoll)
        {
            string question = map.TryGetString("pollQuestion", out var pq) && !string.IsNullOrWhiteSpace(pq)
                ? pq!
                : $"Кто пойдет на событие?";
            string optionsStr = map.TryGetString("pollOptions", out var po) && !string.IsNullOrWhiteSpace(po)
                ? po!
                : "Пойду|Возможно|Не смогу";
            var options = optionsStr.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Take(10).ToList();
            if (options.Count < 2) options = new List<string> { "Да", "Нет" };

            try
            {
                await _sender.SendPoll(chatId, question, options, threadId, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send poll to {Chat}", chatId);
            }
            return;
        }

        try
        {
            await _sender.SendText(chatId, text, parseMode, threadId, noPreview, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send message to {Chat}", chatId);
        }
    }

    private static string HumanizeDuration(TimeSpan span)
    {
        // very small humanizer without dependencies
        var parts = new List<string>(4);
        void add(int value, string one, string many)
        {
            if (value > 0) parts.Add(value == 1 ? $"1 {one}" : $"{value} {many}");
        }

        add(span.Days, "день", "дн.");
        add(span.Hours, "час", "ч");
        add(span.Minutes, "мин", "мин");
        add(span.Seconds, "сек", "сек");

        return parts.Count == 0 ? "мгновение" : string.Join(" ", parts);
    }
}
