namespace TgReminderBot.Models;

public sealed class ChatSettings
{
    public long ChatId { get; set; }
    public int? ControlThreadId { get; set; }
    public int? DefaultReminderThreadId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
