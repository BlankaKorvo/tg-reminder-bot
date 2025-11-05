using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding
{
    /// <summary>
    /// Non-invasive helpers for verifying and republishing command menus without touching the main publisher.
    /// </summary>
    internal static class BotCommandScopesPublisherExtensions
    {
        /// <summary>Force re-publish using existing logic.</summary>
        public static Task RepublishAllAsync(this BotCommandScopesPublisher publisher, CancellationToken ct)
            => publisher.PublishGlobalAsync(ct);

        /// <summary>Verify server menus vs desired and fix drift if needed.</summary>
        public static async Task VerifyAndFixAsync(this BotCommandScopesPublisher publisher, CancellationToken ct)
        {
            // Access private fields (do NOT change publisher class)
            var t = typeof(BotCommandScopesPublisher);
            var bot = (ITelegramBotClient) t.GetField("_bot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(publisher)!;
            var registry = (CommandRegistry) t.GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(publisher)!;
            var super = (SuperAdminConfig) t.GetField("_super", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(publisher)!;

            static bool Has<TAttr>(ICustomAttributeProvider m) where TAttr : Attribute => m.IsDefined(typeof(TAttr), inherit: false);
            static bool IsSuperRestricted(Type tt) => Has<RequireSuperAdminAttribute>(tt) || typeof(IRequireSuperAdmin).IsAssignableFrom(tt);

            static string GetDescriptionFrom(Type handlerType)
            {
                var d1 = handlerType.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;
                if (!string.IsNullOrWhiteSpace(d1)) return d1!;
                var d2 = handlerType.GetCustomAttribute<DisplayAttribute>(inherit: false)?.Description;
                if (!string.IsNullOrWhiteSpace(d2)) return d2!;
                return "—";
            }

            static BotCommand Cmd((string cmd, Type type, bool priv, bool group, bool admin, bool super) x)
                => new BotCommand { Command = x.cmd, Description = GetDescriptionFrom(x.type) };

            // Build desired sets based on registry and attributes
            var items = new List<(string cmd, Type type, bool priv, bool group, bool admin, bool super)>();
            foreach (var kv in registry.Snapshot())
            {
                var type = kv.Value;
                var cmd = kv.Key.TrimStart('/');

                var isPrivateOnly = Has<PrivateOnlyAttribute>(type);
                var isGroupOnly   = Has<RequireGroupAttribute>(type);
                var isAdminOnly   = Has<RequireChatAdminAttribute>(type);
                var isSuperOnly   = IsSuperRestricted(type);

                items.Add((cmd, type, isPrivateOnly, isGroupOnly, isAdminOnly, isSuperOnly));
            }

            
var desiredPrivPublic = items.Where(x => x.priv && !x.super).Select(Cmd)
    .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
    .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray();

var desiredPrivSuper = items.Where(x => x.priv && x.super).Select(Cmd)
    .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
    .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray();


            var desiredGroupEveryone = items.Where(x => x.group && !x.admin && !x.super).Select(Cmd)
                .GroupBy(c => c.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray();

            static bool AreSame(IReadOnlyList<BotCommand> a, IReadOnlyList<BotCommand> b)
            {
                if (a.Count != b.Count) return false;
                for (int i = 0; i < a.Count; i++)
                {
                    if (!string.Equals(a[i].Command, b[i].Command, StringComparison.OrdinalIgnoreCase)) return false;
                    if (!string.Equals(a[i].Description, b[i].Description, StringComparison.Ordinal)) return false;
                }
                return true;
            }

            // Default should be empty
            var def = await bot.GetMyCommands(scope: new BotCommandScopeDefault(), cancellationToken: ct);
            if (def.Count() > 0)
                await bot.DeleteMyCommands(scope: new BotCommandScopeDefault(), cancellationToken: ct);

            

// AllPrivateChats
            var srvPriv = await bot.GetMyCommands(scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);
            var desiredPriv = desiredPrivPublic;
            if (!AreSame(srvPriv.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray(), desiredPriv))
            {
                await bot.DeleteMyCommands(scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);
                if (desiredPriv.Length > 0)
                    await bot.SetMyCommands(desiredPriv, scope: new BotCommandScopeAllPrivateChats(), cancellationToken: ct);
            }

            // Private menu for superadmin only (personal scope)
            if (super is not null && super.Id != 0)
            {
                var superScope = new BotCommandScopeChat { ChatId = super.Id };
                var srvSuper = await bot.GetMyCommands(scope: superScope, cancellationToken: ct);
                if (!AreSame(srvSuper.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray(), desiredPrivSuper))
                {
                    await bot.DeleteMyCommands(scope: superScope, cancellationToken: ct);
                    if (desiredPrivSuper.Length > 0)
                        await bot.SetMyCommands(desiredPrivSuper, scope: superScope, cancellationToken: ct);
                }
            }

            // AllGroupChats


            var srvGrp = await bot.GetMyCommands(scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
            if (!AreSame(srvGrp.OrderBy(c => c.Command, StringComparer.OrdinalIgnoreCase).ToArray(), desiredGroupEveryone))
            {
                await bot.DeleteMyCommands(scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
                await bot.SetMyCommands(desiredGroupEveryone, scope: new BotCommandScopeAllGroupChats(), cancellationToken: ct);
            }
        }
    }
}
