using System.Threading.Tasks;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Models;
using TgReminderBot.Services.Commanding.Abstractions;

namespace TgReminderBot.Services.Commanding.Handlers.Acl;

internal abstract class AclHandlerBase : ICommandHandler, IRequireSuperAdmin
{
    protected readonly ITelegramBotClient Bot;
    protected readonly AppDbContext Db;
    protected readonly SuperAdminConfig Super;

    protected AclHandlerBase(ITelegramBotClient bot, AppDbContext db, SuperAdminConfig super)
    { Bot = bot; Db = db; Super = super; }

    public abstract Task Execute(CommandContext ctx);

    protected bool IsSuper(long userId) => userId == Super.Id;

    protected static bool TryParseUser(string args, long currentUserId, out long id)
    {
        id = 0;
        var a = (args ?? string.Empty).Trim();
        if (string.Equals(a, "me", System.StringComparison.OrdinalIgnoreCase)) { id = currentUserId; return id != 0; }
        return long.TryParse(a, out id);
    }
}