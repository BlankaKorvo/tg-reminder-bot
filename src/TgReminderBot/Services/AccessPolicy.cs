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
        var opt = await _db.AccessOptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct)
                  ?? new AccessOptions { WhitelistEnabled = false };

        var userRules = await _db.AccessRules.AsNoTracking()
            .Where(r => r.Target == AccessTarget.User && r.TargetId == userId)
            .ToListAsync(ct);

        var chatRules = await _db.AccessRules.AsNoTracking()
            .Where(r => r.Target == AccessTarget.Chat && r.TargetId == chatId)
            .ToListAsync(ct);

        if (userRules.Any(r => r.Mode == AccessMode.Deny)) return false;
        if (chatRules.Any(r => r.Mode == AccessMode.Deny)) return false;

        if (!opt.WhitelistEnabled) return true;

        bool allowed =
            userRules.Any(r => r.Mode == AccessMode.Allow) ||
            chatRules.Any(r => r.Mode == AccessMode.Allow);

        return allowed;
    }
}
