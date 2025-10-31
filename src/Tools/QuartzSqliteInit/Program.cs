using Microsoft.Data.Sqlite;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: QuartzSqliteInit <quartz.db> <tables_sqlite.sql>");
    Environment.Exit(2);
}

var dbPath = args[0];
var ddlPath = args[1];

Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var connString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared
}.ToString();

await using var cn = new SqliteConnection(connString);
await cn.OpenAsync();

// Уже есть схема? — выходим.
await using (var check = cn.CreateCommand())
{
    check.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='QRTZ_JOB_DETAILS';";
    if (await check.ExecuteScalarAsync() != null)
    {
        Console.WriteLine("Quartz schema already exists.");
        return;
    }
}

// Читаем официальный скрипт
var sql = await File.ReadAllTextAsync(ddlPath);

// Простейший разбор по ';' (скрипт Quartz простой, без триггеров)
var statements = sql.Replace("\r\n", "\n")
                    .Split(';')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0 && !s.StartsWith("--", StringComparison.Ordinal));

// ВАЖНО: явное приведение к SqliteTransaction
await using var tx = (SqliteTransaction)await cn.BeginTransactionAsync();

foreach (var stmt in statements)
{
    await using var cmd = cn.CreateCommand();
    cmd.Transaction = tx;                  // <-- теперь типы совпадают
    cmd.CommandText = stmt + ';';
    await cmd.ExecuteNonQueryAsync();
}

await tx.CommitAsync();

Console.WriteLine($"Quartz schema installed to: {dbPath}");