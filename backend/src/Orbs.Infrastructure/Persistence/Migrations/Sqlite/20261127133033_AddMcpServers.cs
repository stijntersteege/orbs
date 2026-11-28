using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddMcpServers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_servers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Transport = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ProtectedAuthorization = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_servers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_servers_OwnerId",
                table: "mcp_servers",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_servers");
        }
    }
}
