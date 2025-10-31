namespace TgReminderBot.Models;

public sealed class UserSettings
{
    public long UserId { get; set; }
    public string TimeZone { get; set; } = "Europe/Moscow";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
