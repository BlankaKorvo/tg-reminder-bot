using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding;

public static class CommandRouter
{
    public static async Task Dispatch(Message m, string text, IServiceProvider sp, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CommandRouter");
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        var me  = scope.ServiceProvider.GetRequiredService<BotIdentity>();
        var reg = scope.ServiceProvider.GetRequiredService<CommandRegistry>();
        var middlewares = scope.ServiceProvider.GetServices<ICommandMiddleware>().ToArray();

        var (cmd, args, mine) = Parse(text, me.Username);
        if (!mine || string.IsNullOrEmpty(cmd)) return;

        if (!reg.TryGet(cmd, out var handlerType) || handlerType is null)
        {
            await bot.SendMessage(m.Chat, "Unknown command",
                replyParameters: new ReplyParameters { MessageId = m.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ct);
            return;
        }

        var ctx = new CommandContext(m, args, bot, log, scope.ServiceProvider, ct);

        Func<Task> terminal = async () =>
        {
            var handler = (ICommandHandler)ActivatorUtilities.CreateInstance(scope.ServiceProvider, handlerType);
            await handler.Execute(ctx);
        };

        var pipeline = middlewares.Reverse()
            .Aggregate(terminal, (next, mw) => (Func<Task>)(() => mw.InvokeAsync(ctx, next)));

        await pipeline();
    }

    public static async Task DispatchDocument(Message m, Document doc, IServiceProvider sp, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DocumentRouter");
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        var handlers = scope.ServiceProvider.GetServices<IDocumentHandler>().ToArray();

        var h = handlers.FirstOrDefault(x => x.CanHandle(doc));
        if (h is null)
        {
            await bot.SendMessage(m.Chat, "Unsupported document",
                replyParameters: new ReplyParameters { MessageId = m.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ct);
            return;
        }

        var ctx = new DocumentContext(m, doc, bot, log, scope.ServiceProvider, ct);
        await h.Execute(ctx);
    }

    public static async Task DispatchCallback(CallbackQuery cq, IServiceProvider sp, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CallbackRouter");
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        var handlers = scope.ServiceProvider.GetServices<ICallbackHandler>().ToArray();

        var h = handlers.FirstOrDefault(x => x.CanHandle(cq));
        if (h is null)
        {
            if (cq.Message is { } msg)
                await bot.SendMessage(msg.Chat, "Callback received",
                    replyParameters: new ReplyParameters { MessageId = msg.MessageId, AllowSendingWithoutReply = true },
                    cancellationToken: ct);
            return;
        }

        var ctx = new CallbackContext(cq, bot, log, scope.ServiceProvider, ct);
        await h.Execute(ctx);
    }

    private static (string cmd, string args, bool mine) Parse(string text, string botUsername)
    {
        if (string.IsNullOrWhiteSpace(text) || text[0] != '/') return ("", "", false);
        var idx = text.IndexOf(' ');
        string head = idx >= 0 ? text[..idx] : text;
        string args = idx >= 0 ? text[(idx + 1)..].Trim() : string.Empty;

        var at = head.IndexOf('@');
        if (at >= 0)
        {
            var target = head[(at + 1)..];
            if (!string.Equals(target, botUsername, StringComparison.OrdinalIgnoreCase))
                return ("", "", false);
            head = head[..at];
        }
        return (head.ToLowerInvariant(), args, true);
    }
}
