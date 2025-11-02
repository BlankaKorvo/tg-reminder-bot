using System;

namespace TgReminderBot.Services.Commanding.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class PrivateOnlyAttribute : Attribute { }
}
