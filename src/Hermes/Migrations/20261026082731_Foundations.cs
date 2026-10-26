using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Foundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "messaging",
                columns: table => new
                {
                    condition = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    holding = table.Column<bool>(type: "boolean", nullable: false),
                    last_raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    holds_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => new { x.condition, x.scope });
                    table.CheckConstraint("ck_alerts_condition", "condition IN ('email-submission-failing', 'sms-balance', 'sms-balance-unreadable', 'sms-report-rejected', 'push-auth-failing', 'dispatch-lag', 'job-stalled', 'newer-release', 'configuration-invalid', 'content-key-limit', 'key-unavailable', 'content-rejected', 'observer-failing')");
                });

            migrationBuilder.CreateTable(
                name: "key_versions",
                schema: "messaging",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    check_value = table.Column<byte[]>(type: "bytea", nullable: false),
                    first_made_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_made_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired = table.Column<bool>(type: "boolean", nullable: false),
                    value_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_key_versions", x => new { x.key, x.version });
                    table.CheckConstraint("ck_key_versions_key", "\"key\" IN ('contentKeys', 'hashKeys', 'tokenKeys', 'smsReportKey', 'pushVapidKeys')");
                    table.CheckConstraint("ck_key_versions_value_count", "value_count >= 0");
                    table.CheckConstraint("ck_key_versions_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "token_lifetime",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    longest = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_lifetime", x => x.id);
                    table.CheckConstraint("ck_token_lifetime_id", "id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "key_versions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "token_lifetime",
                schema: "messaging");
        }
    }
}