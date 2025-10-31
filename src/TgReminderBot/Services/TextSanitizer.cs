using System.Text;
using Telegram.Bot.Types.Enums;

namespace TgReminderBot.Services;

public static class TextSanitizer
{
    public static string Sanitize(string text, ParseMode? mode)
    {
        if (string.IsNullOrEmpty(text) || mode is null) return text;

        return mode switch
        {
            ParseMode.MarkdownV2 => EscapeMarkdownV2(text),
            ParseMode.Markdown => EscapeMarkdown(text),
            ParseMode.Html => StripDangerousHtml(text),
            _ => text
        };
    }

    private static string EscapeMarkdown(string s)
    {
        var chars = @"_*\[\]()~`>#+-=|{}.!".ToCharArray();
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (chars.Contains(ch)) sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string EscapeMarkdownV2(string s)
    {
        var chars = @"_*\[\]()~`>#+-=|{}.!".ToCharArray();
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (chars.Contains(ch)) sb.Append('\\');
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string StripDangerousHtml(string s)
    {
        // Very simple allow-list. Telegram strips unknown tags; we just remove angle-brackets where needed.
        // Allowed: b, strong, i, em, u, ins, s, strike, del, a, code, pre, span with class.
        // For safety, replace '<' and '>' not part of allowed tags.
        return s.Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
