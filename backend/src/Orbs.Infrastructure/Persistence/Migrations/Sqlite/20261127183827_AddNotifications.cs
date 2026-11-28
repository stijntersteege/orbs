using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_from",
                table: "users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quiet_time_zone",
                table: "users",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "quiet_until",
                table: "users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Device = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "server_settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_settings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_push_subscriptions_Endpoint",
                table: "push_subscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_subscriptions_OwnerId",
                table: "push_subscriptions",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "server_settings");

            migrationBuilder.DropColumn(
                name: "quiet_from",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quiet_time_zone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "quiet_until",
                table: "users");
        }
    }
}
