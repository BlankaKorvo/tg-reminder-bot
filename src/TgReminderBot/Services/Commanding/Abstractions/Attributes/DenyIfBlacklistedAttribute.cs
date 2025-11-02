using System;

namespace TgReminderBot.Services.Commanding.Abstractions.Attributes
{
    /// <summary>Запрещает выполнение, если пользователь/чат в чёрном списке (Deny). Чёрный приоритет над белым.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class DenyIfBlacklistedAttribute : Attribute { }
}
