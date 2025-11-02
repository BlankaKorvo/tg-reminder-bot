using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;
using TgReminderBot.Common;
using TgReminderBot.Services.Commanding.Abstractions.Attributes;
using System.ComponentModel;

namespace TgReminderBot.Services.Commanding.Handlers.Acl;

[RequireGroup]
[RequireSuperAdmin]
[Command("/acl")]
[Description("Show current ACL settings.")]
internal sealed class AclShowHandler : AclHandlerBase
{
    public AclShowHandler(Telegram.Bot.ITelegramBotClient bot, TgReminderBot.Data.AppDbContext db, SuperAdminConfig s) : base(bot, db, s) { }

    public override async Task Execute(CommandContext ctx)
    {
        var opt = await Db.AccessOptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ctx.CancellationToken)
                  ?? new AccessOptions { Id = 1, WhitelistEnabled = false };

        var rules = await Db.AccessRules.AsNoTracking()
                     .OrderBy(r => r.Target).ThenBy(r => r.TargetId)
                     .Take(30)
                     .ToListAsync(ctx.CancellationToken);

        var sb = new StringBuilder()
            .AppendLine($"Whitelist: `{(opt.WhitelistEnabled ? "ON" : "OFF")}`")
            .AppendLine("Rules (top 30):");

        foreach (var r in rules)
            sb.AppendLine($"- {r.Target} {r.TargetId}: {r.Mode}");

        await Bot.SendMessage(ctx.Message.Chat, sb.ToString().ToMd2(), parseMode: ParseMode.MarkdownV2,
            replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = ctx.Message.MessageId, AllowSendingWithoutReply = true },
            messageThreadId: ctx.ThreadId, cancellationToken: ctx.CancellationToken);
    }
}
