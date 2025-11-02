using System;

namespace TgReminderBot.Services.Commanding.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class CommandAttribute : Attribute
{
    public CommandAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Command name is required", nameof(name));
        Name = name.StartsWith("/") ? name.ToLowerInvariant() : "/" + name.ToLowerInvariant();
    }
    public string Name { get; }
}
