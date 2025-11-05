using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using TgReminderBot.Data;

namespace TgReminderBot.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("202511032012_AddReminders")]
    public partial class AddReminders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    ThreadId = table.Column<int>(type: "INTEGER", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    ParseMode = table.Column<string>(type: "TEXT", nullable: true),
                    RunAt = table.Column<string>(type: "TEXT", nullable: true),
                    Cron = table.Column<string>(type: "TEXT", nullable: true),
                    TimeZone = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Europe/Moscow"),
                    NoPreview = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EventAt = table.Column<string>(type: "TEXT", nullable: true),
                    RemindOffsets = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            // Полезные индексы под выборки по чату/треду и по расписанию
            migrationBuilder.CreateIndex(
                name: "IX_Reminders_ChatId",
                table: "Reminders",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_ChatId_ThreadId",
                table: "Reminders",
                columns: new[] { "ChatId", "ThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_Cron_RunAt",
                table: "Reminders",
                columns: new[] { "Cron", "RunAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Reminders");
        }
    }
}