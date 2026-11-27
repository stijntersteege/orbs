using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrbId",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "guest_orb_ids",
                table: "conversations",
                type: "uuid[]",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrbId",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "guest_orb_ids",
                table: "conversations");
        }
    }
}
