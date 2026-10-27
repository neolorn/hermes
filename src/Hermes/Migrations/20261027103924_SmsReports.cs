using System;

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class SmsReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reported",
                schema: "messaging",
                table: "attempts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "held_reports",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    reference = table.Column<string>(type: "text", nullable: false),
                    matched = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    held_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lapses_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_held_reports", x => x.id);
                    table.CheckConstraint("ck_held_reports_matched", "matched IN ('delivery', 'provider')");
                    table.CheckConstraint("ck_held_reports_outcome", "outcome IN ('delivered', 'failed')");
                });

            migrationBuilder.CreateTable(
                name: "rejected_reports",
                schema: "messaging",
                columns: table => new
                {
                    minute = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rejected_reports", x => x.minute);
                    table.CheckConstraint("ck_rejected_reports_count", "count >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_provider_reference",
                schema: "messaging",
                table: "deliveries",
                column: "provider_reference");

            migrationBuilder.CreateIndex(
                name: "ix_attempts_reference",
                schema: "messaging",
                table: "attempts",
                column: "reference");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attempts_reported",
                schema: "messaging",
                table: "attempts",
                sql: "reported IS NULL OR reported IN ('delivered', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ix_held_reports_lapses_at",
                schema: "messaging",
                table: "held_reports",
                column: "lapses_at");

            migrationBuilder.CreateIndex(
                name: "ix_held_reports_reference",
                schema: "messaging",
                table: "held_reports",
                column: "reference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "held_reports",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "rejected_reports",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "ix_deliveries_provider_reference",
                schema: "messaging",
                table: "deliveries");

            migrationBuilder.DropIndex(
                name: "ix_attempts_reference",
                schema: "messaging",
                table: "attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attempts_reported",
                schema: "messaging",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "reported",
                schema: "messaging",
                table: "attempts");
        }
    }
}