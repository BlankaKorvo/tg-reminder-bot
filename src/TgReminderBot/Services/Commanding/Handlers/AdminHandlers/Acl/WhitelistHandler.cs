using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;

namespace TgReminderBot.Services.Commanding.Handlers.AdminHandlers.Acl;

[PrivateOnly]
[RequireSuperAdmin]
[Command("/whitelist")]
[Description("Enable or disable whitelist mode.")]
internal sealed class WhitelistHandler : AclHandlerBase
{
    public WhitelistHandler(Telegram.Bot.ITelegramBotClient bot, Data.AppDbContext db, Models.SuperAdminConfig s) : base(bot, db, s) { }

    public override async Task Execute(CommandContext ctx)
    {
        var arg = (ctx.Args ?? string.Empty).Trim().ToLowerInvariant();

        var opt = await Db.AccessOptions.FirstOrDefaultAsync(x => x.Id == 1, ctx.CancellationToken)
                  ?? new Models.AccessOptions { Id = 1, WhitelistEnabled = false };

        if (arg is "on" or "1" or "true") opt.WhitelistEnabled = true;
        else if (arg is "off" or "0" or "false") opt.WhitelistEnabled = false;
        else
        {
            await Bot.SendMessage(ctx.Message.Chat, "Usage: /whitelist on|off",
                replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
                cancellationToken: ctx.CancellationToken);
            return;
        }

        Db.AccessOptions.Update(opt);
        await Db.SaveChangesAsync(ctx.CancellationToken);
        await Bot.SendMessage(ctx.Message.Chat, $"Whitelist: {(opt.WhitelistEnabled ? "ON" : "OFF")}",
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            cancellationToken: ctx.CancellationToken);
    }
}
