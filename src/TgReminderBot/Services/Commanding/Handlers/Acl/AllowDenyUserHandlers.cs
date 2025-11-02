using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.Acl;

[RequireGroup]
[RequireSuperAdmin]
[Command("/allowuser")]
[Description("Allow this user to use the bot.")]
internal sealed class AllowUserHandler : AclHandlerBase
{
    public AllowUserHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }
    public override async Task Execute(CommandContext ctx)
    {
        if (!IsSuper(ctx.UserId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Only superadmin",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        if (!TryParseUser(ctx.Args, ctx.UserId, out var userId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Usage: /allowuser <user_id|me>",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var rule = await Db.AccessRules.FirstOrDefaultAsync(x => x.Target == AccessTarget.User && x.TargetId == userId, ctx.CancellationToken);
        if (rule is null)
            await Db.AccessRules.AddAsync(new AccessRule { Target = AccessTarget.User, TargetId = userId, Mode = AccessMode.Allow }, ctx.CancellationToken);
        else
        {
            rule.Mode = AccessMode.Allow;
            Db.AccessRules.Update(rule);
        }
        await Db.SaveChangesAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Allow user {userId}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
[RequireGroup]
[RequireSuperAdmin]
[Command("/denyuser")]
[Description("Deny this user to use the bot.")]
internal sealed class DenyUserHandler : AclHandlerBase
{
    public DenyUserHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }

    public override async Task Execute(CommandContext ctx)
    {
        if (!IsSuper(ctx.UserId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Only superadmin",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        if (!TryParseUser(ctx.Args, ctx.UserId, out var userId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Usage: /denyuser <user_id|me>",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var rule = await Db.AccessRules.FirstOrDefaultAsync(x => x.Target == AccessTarget.User && x.TargetId == userId, ctx.CancellationToken);
        if (rule is null)
            await Db.AccessRules.AddAsync(new AccessRule { Target = AccessTarget.User, TargetId = userId, Mode = AccessMode.Deny }, ctx.CancellationToken);
        else
        {
            rule.Mode = AccessMode.Deny;
            Db.AccessRules.Update(rule);
        }
        await Db.SaveChangesAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Deny user {userId}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
[RequireGroup]
[RequireSuperAdmin]
[Command("/revokeuser")]
[Description("Revoke all ACL rules for this user.")]
internal sealed class RevokeUserHandler : AclHandlerBase
{
    public RevokeUserHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }

    public override async Task Execute(CommandContext ctx)
    {
        if (!IsSuper(ctx.UserId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Only superadmin",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        if (!TryParseUser(ctx.Args, ctx.UserId, out var userId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Usage: /revokeuser <user_id|me>",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var affected = await Db.AccessRules.Where(x => x.Target == AccessTarget.User && x.TargetId == userId).ExecuteDeleteAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Revoked rules for user {userId} ({affected} removed)",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
