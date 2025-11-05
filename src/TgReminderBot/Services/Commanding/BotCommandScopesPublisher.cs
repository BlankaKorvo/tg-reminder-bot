using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding
{
    internal sealed class BotCommandScopesPublisher
    {
        private readonly ITelegramBotClient _bot;
        private readonly CommandRegistry _registry;
        private readonly ILogger<BotCommandScopesPublisher> _log;
        private readonly SuperAdminConfig _super; // у тебя: record SuperAdminConfig(long Id)
        private readonly ConcurrentDictionary<long, byte> _publishedChats = new();

        public BotCommandScopesPublisher(
            ITelegramBotClient bot,
            CommandRegistry registry,
            ILogger<BotCommandScopesPublisher> log,
            SuperAdminConfig super)
        {
            _bot = bot;
            _registry = registry;
            _log = log;
            _super = super;
        }

        private static bool Has<TAttr>(ICustomAttributeProvider t) where TAttr : Attribute =>
            t.IsDefined(typeof(TAttr), inherit: false);

        private static bool IsSuperRestricted(Type t) =>
            Has<RequireSuperAdminAttribute>(t) || typeof(IRequireSuperAdmin).IsAssignableFrom(t);

        private static string GetDescriptionFrom(Type handlerType)
        {
            var d1 = handlerType.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;
            if (!string.IsNullOrWhiteSpace(d1)) return d1!;
            var d2 = handlerType.GetCustomAttribute<DisplayAttribute>(inherit: false)?.Description;
            if (!string.IsNullOrWhiteSpace(d2)) return d2!;
            return string.Empty;
        }

        private static BotCommand Cmd((string cmd, Type type) x) =>
            new BotCommand { Command = x.cmd, Description = GetDescriptionFrom(x.type) };

        private (BotCommand[] privPublic, BotCommand[] privSuper,
                 BotCommand[] groupEveryone, BotCommand[] groupAdmins, BotCommand[] groupAll)
            BuildCommandBuckets()
        {
            var snapshot = _registry.Snapshot(); // "/cmd" -> handler type
            var items = new List<(string cmd, Type type, bool priv, bool group, bool admin, bool super)>();

            foreach (var kv in snapshot)
            {
                var type = kv.Value;
                var cmd = kv.Key.TrimStart('/');

                var isPrivateOnly = Has<PrivateOnlyAttribute>(type);
                var isAdminOnly = Has<RequireChatAdminAttribute>(type);
                var isSuperOnly = IsSuperRestricted(type);

                items.Add((cmd, type,
                    priv: isPrivateOnly,
                    group: !isPrivateOnly,     // всё, что не private — публикуем для групп
                    admin: isAdminOnly,
                    super: isSuperOnly));
            }

            // PRIVATE
            var privPublic = items.Where(x => x.priv && !x.super)
                                  .Select(x => Cmd((x.cmd, x.type)))
                                  .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                  .Select(g => g.First())
                                  .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                  .ToArray();

            var privSuper = items.Where(x => x.priv && x.super)
                                  .Select(x => Cmd((x.cmd, x.type)))
                                  .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                  .Select(g => g.First())
                                  .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                  .ToArray();

            // GROUPS
            var groupEveryone = items.Where(x => x.group && !x.admin && !x.super)
                                     .Select(x => Cmd((x.cmd, x.type)))
                                     .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                     .Select(g => g.First())
                                     .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

            var groupAdmins = items.Where(x => x.group && x.admin && !x.super)
                                     .Select(x => Cmd((x.cmd, x.type)))
                                     .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                     .Select(g => g.First())
                                     .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

            var groupAll = items.Where(x => x.group) // всё групповое (включая админские)
                                     .Select(x => Cmd((x.cmd, x.type)))
                                     .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                     .Select(g => g.First())
                                     .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

            return (privPublic, privSuper, groupEveryone, groupAdmins, groupAll);
        }

        private static bool AreSame(IReadOnlyList<BotCommand> a, IReadOnlyList<BotCommand> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i].Command, b[i].Command, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(a[i].Description ?? string.Empty, b[i].Description ?? string.Empty, StringComparison.Ordinal))
                    return false;
            return true;
        }

        /// <summary>Совместимость: старые вызовы через Extensions делегируют сюда.</summary>
        public Task RepublishAllAsync(CancellationToken ct) => PublishGlobalAsync(ct);

        /// <summary>Переопубликовать все глобальные scope-ы.</summary>
        public async Task PublishGlobalAsync(CancellationToken ct)
        {
            var (privPublic, privSuper, groupEveryone, _, _) = BuildCommandBuckets();

            // AllPrivateChats — только публичные приватные (без super-only)
            var srvPriv = await _bot.GetMyCommands(scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);
            if (!AreSame(srvPriv.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray(), privPublic))
            {
                await _bot.DeleteMyCommands(scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);
                if (privPublic.Length > 0)
                    await _bot.SetMyCommands(privPublic, scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);
            }

            // Личка супер-админа — только super-only приватные
            if (_super is not null && _super.Id != 0)
            {
                var superScope = new BotCommandScopeChat { ChatId = _super.Id };
                var srvSuper = await _bot.GetMyCommands(scope: superScope, cancellationToken: ct);
                if (!AreSame(srvSuper.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray(), privSuper))
                {
                    await _bot.DeleteMyCommands(scope: superScope, cancellationToken: ct);
                    if (privSuper.Length > 0)
                        await _bot.SetMyCommands(privSuper, scope: superScope, cancellationToken: ct);
                }
            }

            // Default — очищаем, чтобы не наслаивалось
            await _bot.DeleteMyCommands(scope: new BotCommandScopeDefault(), cancellationToken: ct);

            // AllGroupChats — только «для всех»
            var srvGroups = await _bot.GetMyCommands(scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
            if (!AreSame(srvGroups.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray(), groupEveryone))
            {
                await _bot.DeleteMyCommands(scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
                if (groupEveryone.Length > 0)
                    await _bot.SetMyCommands(groupEveryone, scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
            }
        }

        /// <summary>Перечень команд per-chat (everyone/admins и «все» для супера).</summary>
        public async Task EnsureChatPublishedAsync(long chatId, CancellationToken ct)
        {
            if (!_publishedChats.TryAdd(chatId, 1))
                return; // уже делали

            var (_, _, groupEveryone, groupAdmins, groupAll) = BuildCommandBuckets();

            _log.LogInformation("Publishing per-chat commands for {ChatId}: everyone={E}, admins={A}, all={All}",
                chatId, groupEveryone.Length, groupAdmins.Length, groupAll.Length);

            await _bot.SetMyCommands(groupEveryone, scope: new BotCommandScopeChat { ChatId = chatId }, cancellationToken: ct);
            await _bot.SetMyCommands(groupAdmins, scope: new BotCommandScopeChatAdministrators { ChatId = chatId }, cancellationToken: ct);

            if (_super is not null && _super.Id != 0)
                await _bot.SetMyCommands(groupAll, scope: new BotCommandScopeChatMember { ChatId = chatId, UserId = _super.Id }, cancellationToken: ct);
        }
    }
}