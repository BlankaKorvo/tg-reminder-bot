using System;
using System.IO;
using System.Net.Http;
using Microsoft.Data.Sqlite;
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
    var me = bot.GetMe().GetAwaiter().GetResult();
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
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(remindersCsb.ConnectionString));

// --------------------------------- Quartz persistent (SQLite-Microsoft, quartz.db)
var quartzPath = builder.Configuration["Storage:QuartzDbPath"] ?? "data/quartz.db";
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
    q.Properties["quartz.serializer.type"] =
        "Quartz.Simpl.SystemTextJsonObjectSerializer, Quartz.Serialization.SystemTextJson";

    q.Properties["quartz.scheduler.instanceName"] = "QuartzScheduler";
    q.Properties["quartz.scheduler.instanceId"] = "AUTO";

    q.Properties["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz";
    q.Properties["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SQLiteDelegate, Quartz";
    q.Properties["quartz.jobStore.useProperties"] = "false";
    q.Properties["quartz.jobStore.tablePrefix"] = "QRTZ_";
    q.Properties["quartz.jobStore.dataSource"] = "default";
    q.Properties["quartz.dataSource.default.provider"] = "SQLite-Microsoft";
    q.Properties["quartz.dataSource.default.connectionString"] = quartzConn;

    // Мы сами разворачиваем схему — выключаем встроенную проверку
    q.Properties["quartz.jobStore.performSchemaValidation"] = "false";
});

builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

// --------------------------------- Доменные сервисы и командный пайплайн
builder.Services.AddSingleton<IAccessPolicy, AccessPolicy>();
builder.Services.AddSingleton<IAccessGuard, AccessGuard>();
builder.Services.AddSingleton<ITelegramSender, TelegramSender>();

// 
builder.Services.AddSingleton<ISchedulingService, SchedulingService>();

builder.Services.AddSingleton<BotCommandScopesPublisher>();
builder.Services.AddCommanding(); 
builder.Services.AddHostedService(sp => new TgReminderBot.Services.BotUpdatesService(
    sp.GetRequiredService<ITelegramBotClient>(),
    sp.GetRequiredService<ILogger<TgReminderBot.Services.BotUpdatesService>>(),
    sp,
    sp.GetRequiredService<BotCommandScopesPublisher>()));

var host = builder.Build();

// Публикация/верификация команд и лог путей БД + развёртывание схемы Quartz
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // 1) сначала применяем миграции
    await db.Database.MigrateAsync();

    // 2) затем — безопасная инициализация дефолтов (если вдруг сид из миграции удалят)
    await db.EnsureAclDefaultsAsync();

    // публикация/верификация команд и т.д.
    var publisher = scope.ServiceProvider.GetRequiredService<BotCommandScopesPublisher>();
    if (builder.Environment.IsDevelopment())
        await publisher.RepublishAllAsync(CancellationToken.None);
    else
        await publisher.VerifyAndFixAsync(CancellationToken.None);

    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
    var resolvedPath = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()!).DataSource;
    log.LogInformation("EF Reminders DB: {Path}", resolvedPath);
    log.LogInformation("Quartz DB: {Path}", quartzPath);

    await TgReminderBot.Services.Quartz.QuartzSqliteSchemaBootstrapper
        .EnsureSqliteSchemaAsync(quartzPath, log, CancellationToken.None);
}

await host.RunAsync();