using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgReminderBot.Services;

public sealed class AccessGuard : IAccessGuard
{
    private readonly ITelegramBotClient _bot;
    private readonly IAccessPolicy _acl;

    public AccessGuard(ITelegramBotClient bot, IAccessPolicy acl)
    {
        _bot = bot;
        _acl = acl;
    }

    public async Task<AccessScope> Authorize(Message m, CancellationToken ct)
    {
        var uid = m.From?.Id ?? 0;
        var chatId = m.Chat.Id;

        if (!await _acl.IsAllowed(uid, chatId, ct))
            return new AccessScope(false, uid, chatId, m.Chat.Type == ChatType.Private);

        if (m.Chat.Type == ChatType.Private)
            return new AccessScope(true, uid, chatId, true);

        var admins = await _bot.GetChatAdministrators(chatId, ct);
        var ok = admins.Any(a => a.User.Id == uid);
        return new AccessScope(ok, uid, chatId, false);
    }
}