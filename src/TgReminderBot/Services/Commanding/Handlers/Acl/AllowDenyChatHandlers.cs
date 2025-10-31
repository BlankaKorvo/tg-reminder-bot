using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Handlers.Acl;

[Command("/allowchat")]
internal sealed class AllowChatHandler : AclHandlerBase
{
    public AllowChatHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }
    public override async Task Execute(CommandContext ctx)
    {
        if (!IsSuper(ctx.UserId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Only superadmin",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var chatId = ctx.ChatId;
        var rule = await Db.AccessRules.FirstOrDefaultAsync(x => x.Target == AccessTarget.Chat && x.TargetId == chatId, ctx.CancellationToken);
        if (rule is null)
            await Db.AccessRules.AddAsync(new AccessRule { Target = AccessTarget.Chat, TargetId = chatId, Mode = AccessMode.Allow }, ctx.CancellationToken);
        else
        {
            rule.Mode = AccessMode.Allow;
            Db.AccessRules.Update(rule);
        }
        await Db.SaveChangesAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Allow chat {chatId}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
[Command("/denychat")]
internal sealed class DenyChatHandler : AclHandlerBase
{
    public DenyChatHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }
    public override async Task Execute(CommandContext ctx)
    {
        if (!IsSuper(ctx.UserId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Only superadmin",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var chatId = ctx.ChatId;
        var rule = await Db.AccessRules.FirstOrDefaultAsync(x => x.Target == AccessTarget.Chat && x.TargetId == chatId, ctx.CancellationToken);
        if (rule is null)
            await Db.AccessRules.AddAsync(new AccessRule { Target = AccessTarget.Chat, TargetId = chatId, Mode = AccessMode.Deny }, ctx.CancellationToken);
        else
        {
            rule.Mode = AccessMode.Deny;
            Db.AccessRules.Update(rule);
        }
        await Db.SaveChangesAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Deny chat {chatId}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
[Command("/revokechat")]
internal sealed class RevokeChatHandler : AclHandlerBase
{
    public RevokeChatHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }
    public override async Task Execute(CommandContext ctx)
    {
        if (!IsSuper(ctx.UserId))
        {
            await Bot.SendMessage(ctx.Message.Chat, "Only superadmin",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        var chatId = ctx.ChatId;
        var affected = await Db.AccessRules.Where(x => x.Target == AccessTarget.Chat && x.TargetId == chatId).ExecuteDeleteAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Revoked rules for chat {chatId} ({affected} removed)",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
