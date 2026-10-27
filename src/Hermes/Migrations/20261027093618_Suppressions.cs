using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Suppressions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suppressions",
                schema: "messaging",
                columns: table => new
                {
                    channel = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash_version = table.Column<int>(type: "integer", nullable: false),
                    made_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppressions", x => new { x.channel, x.category, x.hash });
                    table.CheckConstraint("ck_suppressions_channel", "channel IN ('email', 'sms')");
                    table.CheckConstraint("ck_suppressions_hash_version", "hash_version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_suppressions_hash_version",
                schema: "messaging",
                table: "suppressions",
                column: "hash_version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "suppressions",
                schema: "messaging");
        }
    }
}