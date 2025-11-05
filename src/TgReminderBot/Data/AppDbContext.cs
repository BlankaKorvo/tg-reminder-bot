using Microsoft.EntityFrameworkCore;
using TgReminderBot.Models;

namespace TgReminderBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ChatSettings> ChatSettings => Set<ChatSettings>();
    public DbSet<AccessOptions> AccessOptions => Set<AccessOptions>();
    public DbSet<AccessRule> AccessRules => Set<AccessRule>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Reminder>().HasKey(x => x.Id);
        modelBuilder.Entity<Reminder>().Property(x => x.Id).HasMaxLength(64);
        modelBuilder.Entity<Reminder>().HasIndex(x => new { x.ChatId, x.CreatedBy });

        modelBuilder.Entity<ChatSettings>().HasKey(x => x.ChatId);

        modelBuilder.Entity<AccessRule>().HasKey(x => x.Id);
        modelBuilder.Entity<AccessRule>().HasIndex(x => new { x.Target, x.TargetId });

        modelBuilder.Entity<AccessOptions>().HasKey(x => x.Id);

        modelBuilder.Entity<UserSettings>().HasKey(x => x.UserId);

        // ACL: AccessOptions (одна строка с Id=1)
        modelBuilder.Entity<AccessOptions>(e =>
        {
            e.ToTable("AccessOptions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.WhitelistEnabled).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
        });

        // ACL: AccessRules
        modelBuilder.Entity<AccessRule>(e =>
        {
            e.ToTable("AccessRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Target).IsRequired();
            e.Property(x => x.TargetId).IsRequired();
            e.Property(x => x.Mode).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();

            // быстрый поиск конкретного объекта (пользователь/чат)
            e.HasIndex(x => new { x.Target, x.TargetId });
        });
    }
}
