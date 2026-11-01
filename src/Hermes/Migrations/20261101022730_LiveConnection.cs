using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class LiveConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "away_told",
                schema: "messaging",
                table: "participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "connections",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false),
                    process = table.Column<Guid>(type: "uuid", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_participants_away_told",
                schema: "messaging",
                table: "participants",
                column: "away_told");

            migrationBuilder.CreateIndex(
                name: "ix_connections_last_seen_at",
                schema: "messaging",
                table: "connections",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_connections_person",
                schema: "messaging",
                table: "connections",
                column: "person");

            migrationBuilder.AddForeignKey(
                name: "fk_participants_messages_away_told",
                schema: "messaging",
                table: "participants",
                column: "away_told",
                principalSchema: "messaging",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_participants_messages_away_told",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropTable(
                name: "connections",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "ix_participants_away_told",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "away_told",
                schema: "messaging",
                table: "participants");
        }
    }
}