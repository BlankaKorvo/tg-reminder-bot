using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Middleware;

internal sealed class AuthorizationMiddleware : ICommandMiddleware
{
    private readonly IAccessGuard _guard;
    public AuthorizationMiddleware(IAccessGuard guard) => _guard = guard;

    public async Task InvokeAsync(CommandContext ctx, Func<Task> next)
    {
        var sp = ctx.Services;
        var registry = sp.GetService<CommandRegistry>();
        var me = sp.GetService<BotIdentity>();
        var super = sp.GetService<SuperAdminConfig>();

        var message = ctx.Message;
        var text = message.Text ?? string.Empty;

        // Определяем тип хэндлера по команде (учитывая /cmd@BotName)
        Type? handlerType = null;
        if (registry is not null && !string.IsNullOrWhiteSpace(text) && text.StartsWith('/'))
        {
            var head = text;
            var spc = head.IndexOf(' ');
            if (spc >= 0) head = head[..spc];

            var at = head.IndexOf('@');
            if (at >= 0 && me is not null)
            {
                var target = head[(at + 1)..];
                if (!string.Equals(target, me.Username, StringComparison.OrdinalIgnoreCase))
                    head = string.Empty;
                else
                    head = head[..at];
            }

            if (!string.IsNullOrEmpty(head))
                registry.TryGet(head.ToLowerInvariant(), out handlerType);
        }

        var isSuper = super is not null && ctx.UserId == super.Id;

        // [RequireSuperAdmin]: не-супера режем сразу; супер идёт дальше к проверкам окружения
        if (handlerType?.GetCustomAttribute<RequireSuperAdminAttribute>() is not null && !isSuper)
        {
            await ctx.Bot.SendMessage(message.Chat, "Доступ запрещён: только супер-админ.",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                messageThreadId: message.MessageThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        // [RequireGroup]: только в группах/супергруппах (для супер-админа тоже)
        if (handlerType?.GetCustomAttribute<RequireGroupAttribute>() is not null)
        {
            var type = message.Chat.Type;
            if (type != ChatType.Group && type != ChatType.Supergroup)
            {
                await ctx.Bot.SendMessage(message.Chat, "Эта команда доступна только в группах.",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }

        // [RequireThread]: только внутри темы (для супер-админа тоже)
        if (handlerType?.GetCustomAttribute<RequireThreadAttribute>() is not null)
        {
            if (message.MessageThreadId is null)
            {
                await ctx.Bot.SendMessage(message.Chat, "Эта команда должна выполняться внутри темы.",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }

        // Bypass: супер-админ или [RequireAll] не идут через AccessGuard (но уже прошли env-проверки выше)
        if (isSuper || handlerType?.GetCustomAttribute<RequireAllAttribute>() is not null)
        {
            await next();
            return;
        }

        // Остальные — через централизованный Guard
        var result = await _guard.Authorize(message, ctx.CancellationToken);
        if (!result.Allowed)
        {
            await ctx.Bot.SendMessage(message.Chat, "Access denied",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                messageThreadId: message.MessageThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }

        await next();
    }
}