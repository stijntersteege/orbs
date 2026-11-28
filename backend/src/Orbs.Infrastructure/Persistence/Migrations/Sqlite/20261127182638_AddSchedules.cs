using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrbId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Cron = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FeedUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IntegrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WebhookSecretHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastSeen = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextCheckAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LastRunAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_schedules_orbs_OrbId",
                        column: x => x.OrbId,
                        principalTable: "orbs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_schedules_IsEnabled_NextCheckAt",
                table: "schedules",
                columns: new[] { "IsEnabled", "NextCheckAt" });

            migrationBuilder.CreateIndex(
                name: "IX_schedules_OrbId",
                table: "schedules",
                column: "OrbId");

            migrationBuilder.CreateIndex(
                name: "IX_schedules_OwnerId",
                table: "schedules",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedules");
        }
    }
}
