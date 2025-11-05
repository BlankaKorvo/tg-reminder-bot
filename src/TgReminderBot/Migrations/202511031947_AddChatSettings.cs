#nullable disable
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using TgReminderBot.Data;

namespace TgReminderBot.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("202511031947_AddChatSettings")]
    public partial class AddChatSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatSettings",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    ControlThreadId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultReminderThreadId = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSettings", x => x.ChatId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChatSettings");
        }
    }
}