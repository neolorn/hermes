using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class InAppList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rate_counters_channel",
                schema: "messaging",
                table: "rate_counters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_preferences_channel",
                schema: "messaging",
                table: "preferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_places_channel",
                schema: "messaging",
                table: "places");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notices_status",
                schema: "messaging",
                table: "notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliveries_channel",
                schema: "messaging",
                table: "deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliveries_status",
                schema: "messaging",
                table: "deliveries");

            migrationBuilder.AddColumn<string>(
                name: "parts",
                schema: "messaging",
                table: "deliveries",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entries",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    written_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target = table.Column<string>(type: "text", nullable: true),
                    parts = table.Column<string>(type: "jsonb", nullable: false),
                    content_key_version = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_entries_deliveries",
                        column: x => x.delivery_id,
                        principalSchema: "messaging",
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rate_counters_channel",
                schema: "messaging",
                table: "rate_counters",
                sql: "channel IN ('email', 'sms', 'push', 'inapp')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_preferences_channel",
                schema: "messaging",
                table: "preferences",
                sql: "channel IN ('email', 'sms', 'push', 'inapp')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_places_channel",
                schema: "messaging",
                table: "places",
                sql: "channel IN ('email', 'sms', 'push', 'inapp')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notices_status",
                schema: "messaging",
                table: "notices",
                sql: "status IN ('waiting', 'inflight', 'sent', 'delivered', 'failed', 'cancelled', 'expired', 'suppressed', 'optedout', 'noconsent', 'limited', 'balancefloor', 'nosubscription', 'read')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliveries_channel",
                schema: "messaging",
                table: "deliveries",
                sql: "channel IN ('email', 'sms', 'push', 'inapp')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliveries_status",
                schema: "messaging",
                table: "deliveries",
                sql: "status IN ('waiting', 'inflight', 'sent', 'delivered', 'failed', 'cancelled', 'expired', 'suppressed', 'optedout', 'noconsent', 'limited', 'balancefloor', 'nosubscription', 'read')");

            migrationBuilder.CreateIndex(
                name: "ix_entries_content_key_version",
                schema: "messaging",
                table: "entries",
                column: "content_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_entries_delivery_id",
                schema: "messaging",
                table: "entries",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_person_written_at",
                schema: "messaging",
                table: "entries",
                columns: new[] { "person", "written_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_written_at",
                schema: "messaging",
                table: "entries",
                column: "written_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entries",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rate_counters_channel",
                schema: "messaging",
                table: "rate_counters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_preferences_channel",
                schema: "messaging",
                table: "preferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_places_channel",
                schema: "messaging",
                table: "places");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notices_status",
                schema: "messaging",
                table: "notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliveries_channel",
                schema: "messaging",
                table: "deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliveries_status",
                schema: "messaging",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "parts",
                schema: "messaging",
                table: "deliveries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rate_counters_channel",
                schema: "messaging",
                table: "rate_counters",
                sql: "channel IN ('email', 'sms', 'push')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_preferences_channel",
                schema: "messaging",
                table: "preferences",
                sql: "channel IN ('email', 'sms', 'push')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_places_channel",
                schema: "messaging",
                table: "places",
                sql: "channel IN ('email', 'sms', 'push')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notices_status",
                schema: "messaging",
                table: "notices",
                sql: "status IN ('waiting', 'inflight', 'sent', 'delivered', 'failed', 'cancelled', 'expired', 'suppressed', 'optedout', 'noconsent', 'limited', 'balancefloor', 'nosubscription')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliveries_channel",
                schema: "messaging",
                table: "deliveries",
                sql: "channel IN ('email', 'sms', 'push')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliveries_status",
                schema: "messaging",
                table: "deliveries",
                sql: "status IN ('waiting', 'inflight', 'sent', 'delivered', 'failed', 'cancelled', 'expired', 'suppressed', 'optedout', 'noconsent', 'limited', 'balancefloor', 'nosubscription')");
        }
    }
}