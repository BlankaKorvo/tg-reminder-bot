using System;

namespace TgReminderBot.Models;

public sealed class AccessOptions
{
    /// <summary>Единственная строка настроек ACL (Id = 1).</summary>
    public int Id { get; set; } = 1;

    /// <summary>Если true — включён белый список (whitelist).</summary>
    public bool WhitelistEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}