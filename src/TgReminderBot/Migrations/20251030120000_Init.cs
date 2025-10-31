using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TgReminderBot.Migrations
{
    public partial class AddAccessOptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WhitelistEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessOptions", x => x.Id);
                });

            // начальная строка (одиночная запись с Id=1)
            migrationBuilder.InsertData(
                table: "AccessOptions",
                columns: new[] { "Id", "WhitelistEnabled", "UpdatedAt" },
                values: new object[] { 1, false, DateTimeOffset.UtcNow }
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AccessOptions");
        }
    }
}