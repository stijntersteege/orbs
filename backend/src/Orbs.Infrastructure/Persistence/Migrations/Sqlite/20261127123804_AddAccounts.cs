using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "providers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "orbs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "conversations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "context_sources",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_logins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_logins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passkeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "BLOB", maxLength: 1024, nullable: false),
                    PublicKey = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SignCount = table.Column<uint>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passkeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_passkeys_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LastSeenAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_providers_OwnerId",
                table: "providers",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_orbs_OwnerId",
                table: "orbs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_OwnerId",
                table: "conversations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_context_sources_OwnerId",
                table: "context_sources",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_Issuer_Subject",
                table: "external_logins",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_UserId",
                table: "external_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_passkeys_CredentialId",
                table: "passkeys",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_passkeys_UserId",
                table: "passkeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_TokenHash",
                table: "sessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_UserId",
                table: "sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_logins");

            migrationBuilder.DropTable(
                name: "passkeys");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_providers_OwnerId",
                table: "providers");

            migrationBuilder.DropIndex(
                name: "IX_orbs_OwnerId",
                table: "orbs");

            migrationBuilder.DropIndex(
                name: "IX_conversations_OwnerId",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_context_sources_OwnerId",
                table: "context_sources");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "orbs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "context_sources");
        }
    }
}
