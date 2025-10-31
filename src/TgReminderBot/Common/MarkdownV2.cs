namespace TgReminderBot.Common;

public static class MarkdownV2
{
    /// <summary>
    /// Экранирует строку по правилам Telegram MarkdownV2.
    /// Экранит \ _ * [ ] ( ) ~ ` > # + - = | { } . !
    /// </summary>
    public static string ToMd2(this string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s
            .Replace(@"\", @"\\")
            .Replace("_", @"\_")
            .Replace("*", @"\*")
            .Replace("[", @"\[")
            .Replace("]", @"\]")
            .Replace("(", @"\(")
            .Replace(")", @"\)")
            .Replace("~", @"\~")
            .Replace("`", @"\`")
            .Replace(">", @"\>")
            .Replace("#", @"\#")
            .Replace("+", @"\+")
            .Replace("-", @"\-")
            .Replace("=", @"\=")
            .Replace("|", @"\|")
            .Replace("{", @"\{")
            .Replace("}", @"\}")
            .Replace(".", @"\.")
            .Replace("!", @"\!");
    }

    /// <summary>
    /// Безопасная ссылка: экранирует текст (влево), и минимально экранирует URL (закрывающую скобку/бэкслэш).
    /// </summary>
    public static string ToMd2Link(string text, string url)
    {
        if (url is null) url = "";
        // В URL в MarkdownV2 критичны только ')' и '\'
        var safeUrl = url.Replace(@"\", @"\\").Replace(")", @"\)");
        return $"[{text.ToMd2()}]({safeUrl})";
    }
}