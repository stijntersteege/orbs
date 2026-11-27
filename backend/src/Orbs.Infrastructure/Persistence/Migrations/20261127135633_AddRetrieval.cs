using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "providers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IndexedAt",
                table: "context_sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndexedChunks",
                table: "context_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "context_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Vector = table.Column<byte[]>(type: "bytea", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_context_chunks_context_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "context_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_context_chunks_OwnerId",
                table: "context_chunks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_context_chunks_SourceId_Index",
                table: "context_chunks",
                columns: new[] { "SourceId", "Index" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "context_chunks");

            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                table: "context_sources");

            migrationBuilder.DropColumn(
                name: "IndexedChunks",
                table: "context_sources");
        }
    }
}
