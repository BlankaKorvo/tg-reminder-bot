using System;

namespace TgReminderBot.Models;

public enum AccessTarget
{
    User = 1,
    Chat = 2
}

public enum AccessMode
{
    Allow = 1,
    Deny = 2
}

public sealed class AccessRule
{
    public long Id { get; set; }

    public AccessTarget Target { get; set; }
    public long TargetId { get; set; }

    public AccessMode Mode { get; set; }

    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}