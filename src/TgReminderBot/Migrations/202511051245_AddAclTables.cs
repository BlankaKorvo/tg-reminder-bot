using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TgReminderBot.Data;

#nullable disable

namespace TgReminderBot.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("202511051245_AddAclTables")]
    public partial class AddAclTables_20251105_1245 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AccessOptions (single-row settings table; Id is NOT autoincrement)
            migrationBuilder.CreateTable(
                name: "AccessOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    WhitelistEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessOptions", x => x.Id);
                });

            // Seed default row via raw SQL to avoid model-mapped InsertData requirement
            var nowIso = DateTimeOffset.UtcNow.ToString("o");
            migrationBuilder.Sql($@"INSERT OR IGNORE INTO AccessOptions (Id, WhitelistEnabled, UpdatedAt)
                                    VALUES (1, 0, '{nowIso}');");

            // AccessRules
            migrationBuilder.CreateTable(
                name: "AccessRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                              .Annotation("Sqlite:Autoincrement", true),
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<long>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRules_Target_TargetId",
                table: "AccessRules",
                columns: new[] { "Target", "TargetId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AccessRules");
            migrationBuilder.DropTable(name: "AccessOptions");
        }
    }
}
