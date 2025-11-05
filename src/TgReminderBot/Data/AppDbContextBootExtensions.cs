using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using TgReminderBot.Models;

namespace TgReminderBot.Data;

public static class AppDbContextBootExtensions
{
    public static async Task EnsureAclDefaultsAsync(this AppDbContext db, CancellationToken ct = default)
    {
        // Правильная проверка существования таблицы через ExecuteScalar,
        // т.к. ExecuteSqlRaw для SELECT возвращает -1 (а не COUNT(*)). 
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'AccessOptions' AND type = 'table';";
        var countObj = await cmd.ExecuteScalarAsync(ct);
        var count = 0L;
        if (countObj is long l) count = l;
        else if (countObj is int i) count = i;

        var hasTable = count > 0;
        if (!hasTable)
            return;

        // 2) Если строки настроек ещё нет — создадим с Id=1
        var exists = await db.AccessOptions.AnyAsync(x => x.Id == 1, ct);
        if (!exists)
        {
            db.AccessOptions.Add(new AccessOptions { Id = 1, WhitelistEnabled = false });
            await db.SaveChangesAsync(ct);
        }
    }
}
