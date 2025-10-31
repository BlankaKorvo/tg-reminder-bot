using Telegram.Bot.Types;

namespace TgReminderBot.Services;

public interface IAccessGuard
{
    Task<AccessScope> Authorize(Message m, CancellationToken ct);
}

public sealed record AccessScope(bool Allowed, long UserId, long ChatId, bool IsPrivate)
{
    public IQueryable<TgReminderBot.Models.Reminder> Apply(IQueryable<TgReminderBot.Models.Reminder> q)
        => q.Where(r => r.ChatId == ChatId && r.CreatedBy == UserId);
}