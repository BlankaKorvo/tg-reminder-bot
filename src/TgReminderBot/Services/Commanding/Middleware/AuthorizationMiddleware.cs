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

        //      ( /cmd@BotName)
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


        // [RequireSuperAdmin]: superadmin only
        if (handlerType?.GetCustomAttribute<RequireSuperAdminAttribute>() is not null && !isSuper)
        {
            await ctx.Bot.SendMessage(message.Chat, "Access denied: superadmin only.",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                messageThreadId: message.MessageThreadId,
                cancellationToken: ctx.CancellationToken);
            return;
        }



        // [PrivateOnly]: команда доступна только в личных сообщениях
        if (handlerType?.GetCustomAttribute<PrivateOnlyAttribute>() is not null)
        {
            if (message.Chat.Type != ChatType.Private)
            {
                await ctx.Bot.SendMessage(message.Chat, "This command is available only in private chat.",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }


        // [PrivateOnly]: private chats only
        if (handlerType?.GetCustomAttribute<PrivateOnlyAttribute>() is not null)
        {
            if (message.Chat.Type != ChatType.Private)
            {
                await ctx.Bot.SendMessage(message.Chat, "This command is available only in private chat.",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }

        // [RequireGroup]:   / ( - )
        if (handlerType?.GetCustomAttribute<RequireGroupAttribute>() is not null)
        {
            var type = message.Chat.Type;
            if (type != ChatType.Group && type != ChatType.Supergroup)
            {
                await ctx.Bot.SendMessage(message.Chat, "     .",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }

        // [RequireThread]:    ( - )
        if (handlerType?.GetCustomAttribute<RequireThreadAttribute>() is not null)
        {
            if (message.MessageThreadId is null)
            {
                await ctx.Bot.SendMessage(message.Chat, "     .",
                    replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = message.MessageId, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId,
                    cancellationToken: ctx.CancellationToken);
                return;
            }
        }

        // Bypass: -  [RequireAll]    AccessGuard (   env- )
        if (isSuper || handlerType?.GetCustomAttribute<RequireAllAttribute>() is not null)
        {
            await next();
            return;
        }

        //     Guard
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