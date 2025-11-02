using System;

namespace TgReminderBot.Services.Commanding.Abstractions.Attributes
{
    /// <summary>
    /// Жёсткое требование супер-админа для команды. Если атрибут присутствует — доступ только у SuperAdmin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class RequireSuperAdminAttribute : Attribute { }
}
