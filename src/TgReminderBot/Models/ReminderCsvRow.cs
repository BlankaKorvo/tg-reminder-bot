namespace TgReminderBot.Services;

public sealed class ReminderCsvRow
{
    public int? ThreadId { get; set; }
    public string? Text { get; set; }

    // Храним как строку (ISO-8601, "2025-11-01T09:00:00+03:00" или локальное время для твоего парсера)
    public string? RunAt { get; set; }

    // В CSV оставляем старое имя колонки для совместимости
    public string? TimeZoneId { get; set; }       // будет мапиться в Reminder.TimeZone
    public string? OffsetsCsv { get; set; }       // будет мапиться в Reminder.RemindOffsets

    public string? ParseMode { get; set; }
    public bool NoPreview { get; set; }

    public static ReminderCsvRow FromEntity(Models.Reminder r) => new()
    {
        ThreadId = r.ThreadId,
        Text = r.Text,
        RunAt = r.RunAt,
        TimeZoneId = r.TimeZone,
        OffsetsCsv = r.RemindOffsets,
        ParseMode = r.ParseMode,
        NoPreview = r.NoPreview
    };
}