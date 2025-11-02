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
    /// <summary>
    /// Publishes Telegram command menus by scope.
    /// - AllPrivateChats: only strictly private commands
    /// - AllGroupChats:   base set visible to everyone in groups
    /// - Per chat: Chat (everyone), ChatAdministrators (admins), ChatMember (superadmin)
    /// </summary>
    public sealed class BotCommandScopesPublisher
    {
        private readonly ITelegramBotClient _bot;
        private readonly ILogger<BotCommandScopesPublisher> _log;
        private readonly CommandRegistry _registry;
        private readonly SuperAdminConfig _super;
        private readonly ConcurrentDictionary<long, byte> _publishedChats = new();

        public BotCommandScopesPublisher(
            ITelegramBotClient bot,
            ILogger<BotCommandScopesPublisher> log,
            CommandRegistry registry,
            SuperAdminConfig super)
        {
            _bot = bot;
            _log = log;
            _registry = registry;
            _super = super;
        }

        private static bool Has<TAttr>(ICustomAttributeProvider t) where TAttr : Attribute =>
            t.IsDefined(typeof(TAttr), inherit: false);

        private static bool IsSuperRestricted(Type t) =>
            Has<RequireSuperAdminAttribute>(t) || typeof(IRequireSuperAdmin).IsAssignableFrom(t);

        // Read description from class attributes or fallback.
        private static string GetDescriptionFrom(Type handlerType)
        {
            var d1 = handlerType.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;
            if (!string.IsNullOrWhiteSpace(d1)) return d1!;
            var d2 = handlerType.GetCustomAttribute<DisplayAttribute>(inherit: false)?.Description;
            if (!string.IsNullOrWhiteSpace(d2)) return d2!;
            return "—";
        }

        private (BotCommand[] priv, BotCommand[] groupEveryone, BotCommand[] groupAdmins, BotCommand[] groupAll)
            BuildCommandBuckets()
        {
            var snapshot = _registry.Snapshot(); // "/cmd" -> handler type
            var items = new List<(string cmd, Type type, bool priv, bool group, bool admin, bool super)>();

            foreach (var kv in snapshot)
            {
                var type = kv.Value;
                var cmd = kv.Key.TrimStart('/');

                var isPrivateOnly = Has<PrivateOnlyAttribute>(type);
                var isGroupOnly   = Has<RequireGroupAttribute>(type);
                var isAdminOnly   = Has<RequireChatAdminAttribute>(type);
                var isSuperOnly   = IsSuperRestricted(type);

                items.Add((cmd, type, isPrivateOnly, isGroupOnly, isAdminOnly, isSuperOnly));
            }

            static BotCommand MakeCmd((string cmd, Type type, bool priv, bool group, bool admin, bool super) x)
                => new BotCommand { Command = x.cmd, Description = GetDescriptionFrom(x.type) ?? "—" };

            // Private DM list: only strictly private-marked commands
            var privateCmds = items
                .Where(x => x.priv)
                .Select(MakeCmd)
                .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .ToArray();

            // Group buckets
            var groupEveryone = items
                .Where(x => x.group && !x.admin && !x.super)
                .Select(MakeCmd)
                .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .ToArray();

            var groupAdmins = items
                .Where(x => x.group && !x.super) // include everyone + admin-only
                .Select(MakeCmd)
                .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .ToArray();

            // All group commands including superadmin-only (used for ChatMember(superadmin))
            var groupAll = items
                .Where(x => x.group || x.admin || x.super)
                .Select(MakeCmd)
                .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .ToArray();

            return (privateCmds, groupEveryone, groupAdmins, groupAll);
        }

        /// <summary>Publish global menus for private and group chats.</summary>
        public async Task PublishGlobalAsync(CancellationToken ct)
        {
            var (priv, groupEveryone, _, _) = BuildCommandBuckets();
            _log.LogInformation("Publishing global commands: private={Priv}, groups(everyone)={Grp}",
                priv.Length, groupEveryone.Length);

            // Clear default so it doesn't bleed into scoped menus
            await _bot.DeleteMyCommands(scope: new BotCommandScopeDefault(), cancellationToken: ct);

            if (priv.Length > 0)
                await _bot.SetMyCommands(priv, scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);

            await _bot.SetMyCommands(groupEveryone, scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
        }

        /// <summary>
        /// Ensure per-chat menus are published once for the given chat id.
        /// Sets:
        ///   - Chat (visible to everyone in chat)
        ///   - ChatAdministrators (visible to admins of this chat)
        ///   - ChatMember(superAdmin) (visible only to configured superadmin)
        /// </summary>
        public async Task EnsureChatPublishedAsync(long chatId, CancellationToken ct)
        {
            if (!_publishedChats.TryAdd(chatId, 1))
                return; // already published

            var (_, groupEveryone, groupAdmins, groupAll) = BuildCommandBuckets();

            _log.LogInformation("Publishing per-chat commands for {ChatId}: everyone={E}, admins={A}, all={All}",
                chatId, groupEveryone.Length, groupAdmins.Length, groupAll.Length);

            await _bot.SetMyCommands(groupEveryone, scope: new BotCommandScopeChat { ChatId = chatId }, cancellationToken: ct);
            await _bot.SetMyCommands(groupAdmins, scope: new BotCommandScopeChatAdministrators { ChatId = chatId }, cancellationToken: ct);

            if (_super is not null && _super.Id != 0)
                await _bot.SetMyCommands(groupAll, scope: new BotCommandScopeChatMember { ChatId = chatId, UserId = _super.Id }, cancellationToken: ct);
        }
    }
}
