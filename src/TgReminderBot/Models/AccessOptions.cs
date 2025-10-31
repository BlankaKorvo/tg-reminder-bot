namespace TgReminderBot.Models;

public class AccessOptions
{
    public int Id { get; set; } = 1;
    public bool WhitelistEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
