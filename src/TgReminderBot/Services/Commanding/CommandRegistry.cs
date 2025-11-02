using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, Type> _map;

    internal CommandRegistry(Assembly asm)
    {
        _map = new(StringComparer.OrdinalIgnoreCase);

        var handlerType = typeof(ICommandHandler);
        var handlers = asm.GetTypes().Where(t =>
            t.IsClass && !t.IsAbstract && handlerType.IsAssignableFrom(t) &&
            t.Namespace != null && t.Namespace.StartsWith("TgReminderBot.Services.Commanding.Handlers", StringComparison.Ordinal));

        foreach (var t in handlers)
        {
            var attributes = t.GetCustomAttributes(typeof(CommandAttribute), false).Cast<CommandAttribute>().ToArray();
            if (attributes.Length == 0) continue;

            foreach (var attr in attributes)
                _map[attr.Name] = t;
        }
    }

    internal bool TryGet(string cmd, out Type? handlerType) => _map.TryGetValue(cmd, out handlerType);
    internal IReadOnlyDictionary<string, Type> Snapshot() => _map;
}
