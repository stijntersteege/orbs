using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "attached_source_ids",
                table: "conversations",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attached_source_ids",
                table: "conversations");
        }
    }
}
