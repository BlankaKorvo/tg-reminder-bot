using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Middleware;

namespace TgReminderBot.Services.Commanding;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommanding(this IServiceCollection services, Assembly? asm = null)
    {
        asm ??= typeof(CommandRouter).Assembly;

        // Middlewares (order matters)
        services.AddSingleton<ICommandMiddleware, LoggingMiddleware>();
        services.AddSingleton<ICommandMiddleware, AuthorizationMiddleware>();
        services.AddSingleton<ICommandMiddleware, ThrottlingMiddleware>();

        // Register all handlers as Transient
        var handlerType = typeof(ICommandHandler);
        var types = asm.GetTypes().Where(t =>
            t.IsClass && !t.IsAbstract && handlerType.IsAssignableFrom(t) &&
            t.Namespace != null && t.Namespace.StartsWith("TgReminderBot.Services.Commanding.Handlers", StringComparison.Ordinal));
        foreach (var t in types)
            services.AddTransient(handlerType, t);

        // Document & Callback handlers
        void ScanTransient<TService>()
        {
            var serviceType = typeof(TService);
            var ts = asm.GetTypes().Where(t =>
                t.IsClass && !t.IsAbstract && serviceType.IsAssignableFrom(t) &&
                t.Namespace != null && t.Namespace.StartsWith("TgReminderBot.Services.Commanding.Handlers", StringComparison.Ordinal));
            foreach (var t in ts) services.AddTransient(serviceType, t);
        }
        ScanTransient<IDocumentHandler>();
        ScanTransient<ICallbackHandler>();

        // Registry from attributes
        services.AddSingleton(new CommandRegistry(asm));

        return services;
    }
}
