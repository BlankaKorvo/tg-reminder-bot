using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TgReminderBot.Data;
using TgReminderBot.Models;

namespace TgReminderBot.Services;

public sealed class AccessPolicy : IAccessPolicy
{
    private readonly AppDbContext _db;
    public AccessPolicy(AppDbContext db) => _db = db;

    public async Task<bool> IsAllowed(long userId, long chatId, CancellationToken ct)
    {
        AccessOptions opt;
        try
        {
            opt = await _db.AccessOptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct)
                  ?? new AccessOptions { WhitelistEnabled = false };
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table"))
        {
            // База ещё не полностью мигрирована — считаем whitelist выключенным.
            opt = new AccessOptions { WhitelistEnabled = false };
        }

        // ВАЖНО: SQLite не поддерживает ORDER BY по DateTimeOffset.
        // В исходном коде правила сортировались по CreatedAt и забирались списком,
        // но далее использовались только проверки Any().
        // Переписываем на точечные AnyAsync без сортировок и без materialize списков.

        // 1) Явные запреты имеют максимальный приоритет.
        if (await _db.AccessRules.AsNoTracking()
                .AnyAsync(r => r.Target == AccessTarget.User && r.TargetId == userId && r.Mode == AccessMode.Deny, ct))
            return false;

        if (await _db.AccessRules.AsNoTracking()
                .AnyAsync(r => r.Target == AccessTarget.Chat && r.TargetId == chatId && r.Mode == AccessMode.Deny, ct))
            return false;

        // 2) Если whitelist выключен — доступ разрешён.
        if (!opt.WhitelistEnabled) return true;

        // 3) При включённом whitelist — достаточно одного разрешающего правила для пользователя или чата.
        var userAllowed = await _db.AccessRules.AsNoTracking()
            .AnyAsync(r => r.Target == AccessTarget.User && r.TargetId == userId && r.Mode == AccessMode.Allow, ct);

        if (userAllowed) return true;

        var chatAllowed = await _db.AccessRules.AsNoTracking()
            .AnyAsync(r => r.Target == AccessTarget.Chat && r.TargetId == chatId && r.Mode == AccessMode.Allow, ct);

        return chatAllowed;
    }
}
