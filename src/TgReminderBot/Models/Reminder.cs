namespace TgReminderBot.Models;

public class Reminder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long ChatId { get; set; }
    public int? ThreadId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? ParseMode { get; set; }
    public string? RunAt { get; set; }
    public string? Cron { get; set; }
    public string TimeZone { get; set; } = "Europe/Moscow";
    public bool NoPreview { get; set; } = true;
    public string? EventAt { get; set; }
    public string? RemindOffsets { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long? CreatedBy { get; set; }
}
