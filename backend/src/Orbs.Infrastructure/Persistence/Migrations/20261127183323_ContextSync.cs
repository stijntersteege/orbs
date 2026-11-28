using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContextSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextSyncAt",
                table: "context_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncEvery",
                table: "context_sources",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_context_sources_NextSyncAt",
                table: "context_sources",
                column: "NextSyncAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_context_sources_NextSyncAt",
                table: "context_sources");

            migrationBuilder.DropColumn(
                name: "NextSyncAt",
                table: "context_sources");

            migrationBuilder.DropColumn(
                name: "SyncEvery",
                table: "context_sources");
        }
    }
}
