using System;
using System.IO;
using System.Net.Http;
using Microsoft.Data.Sqlite;                  // единый стек SQLite
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;
using TgReminderBot.Data;
using TgReminderBot.Models;
using TgReminderBot.Services;
using TgReminderBot.Services.Commanding;

var builder = Host.CreateApplicationBuilder(args);

// --------------------------------- Логирование
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
});

// --------------------------------- Telegram Bot Client
builder.Services.AddHttpClient("tg");
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var token = builder.Configuration["Bot:Token"]
                ?? Environment.GetEnvironmentVariable("BOT_TOKEN")
                ?? throw new InvalidOperationException("Bot:Token/BOT_TOKEN not configured");

    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("tg");
    return new TelegramBotClient(token, http);
});

// --------------------------------- Bot identity for routers/middlewares
builder.Services.AddSingleton<BotIdentity>(sp =>
{
    var bot = sp.GetRequiredService<ITelegramBotClient>();
    var me = bot.GetMe().GetAwaiter().GetResult(); // <-- вместо GetMeAsync()
    return new BotIdentity(me.Id, me.Username ?? string.Empty);
});

// --------------------------------- Суперадмин (DI)
var superAdminRaw = builder.Configuration["Bot:SuperAdminId"]
                   ?? Environment.GetEnvironmentVariable("SUPERADMIN_ID");

long.TryParse(superAdminRaw, out var superAdminId);
builder.Services.AddSingleton(new SuperAdminConfig(superAdminId));


// --------------------------------- EF Core SQLite (reminders.db)
var dbPath = builder.Configuration["Storage:DbPath"] ?? "data/reminders.db";
if (!Path.IsPathRooted(dbPath))
    dbPath = Path.Combine(AppContext.BaseDirectory, dbPath.Replace('/', Path.DirectorySeparatorChar));
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var remindersCsb = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared
};
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(remindersCsb.ConnectionString));

// --------------------------------- Quartz persistent (SQLite-Microsoft, quartz.db)
var quartzPath = builder.Configuration["Quartz:DbPath"] ?? "data/quartz.db";
if (!Path.IsPathRooted(quartzPath))
    quartzPath = Path.Combine(AppContext.BaseDirectory, quartzPath.Replace('/', Path.DirectorySeparatorChar));
Directory.CreateDirectory(Path.GetDirectoryName(quartzPath)!);

var quartzConn = new SqliteConnectionStringBuilder
{
    DataSource = quartzPath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Cache = SqliteCacheMode.Shared
}.ToString();

builder.Services.AddQuartz(q =>
{
    // ЯВНО указываем сериализатор System.Text.Json (правильный тип):
    q.Properties["quartz.serializer.type"] =
        "Quartz.Simpl.SystemTextJsonObjectSerializer, Quartz.Serialization.SystemTextJson";

    q.Properties["quartz.scheduler.instanceName"] = "QuartzScheduler";
    q.Properties["quartz.scheduler.instanceId"] = "AUTO";
    q.Properties["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz";
    q.Properties["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SQLiteDelegate, Quartz";
    q.Properties["quartz.jobStore.useProperties"] = "true";
    q.Properties["quartz.jobStore.tablePrefix"] = "QRTZ_";
    q.Properties["quartz.jobStore.dataSource"] = "default";
    q.Properties["quartz.dataSource.default.provider"] = "SQLite-Microsoft";
    q.Properties["quartz.dataSource.default.connectionString"] = quartzConn;
});

builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

// --------------------------------- Доменные сервисы и команды
builder.Services.AddSingleton<IAccessPolicy, AccessPolicy>();
builder.Services.AddSingleton<IAccessGuard, AccessGuard>();
builder.Services.AddSingleton<ITelegramSender, TelegramSender>();
builder.Services.AddSingleton<ISchedulingService, SchedulingService>();

builder.Services.AddHostedService<BotUpdatesService>();
builder.Services.AddCommanding();

// --------------------------------- Bootstrap + миграции EF
var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Было: await db.Database.MigrateAsync();
    await db.Database.EnsureCreatedAsync();

    // Гарантируем строку AccessOptions с Id=1
    if (!await db.AccessOptions.AnyAsync(a => a.Id == 1))
    {
        db.AccessOptions.Add(new AccessOptions
        {
            Id = 1,
            WhitelistEnabled = false,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
    var resolvedPath = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()!).DataSource;
    log.LogInformation("EF Reminders DB: {Path}", resolvedPath);
    log.LogInformation("Quartz DB: {Path}", quartzPath);
}

await host.RunAsync();