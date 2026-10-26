using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class SendAndDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency",
                schema: "messaging",
                columns: table => new
                {
                    kind = table.Column<string>(type: "text", nullable: false),
                    anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    scope = table.Column<byte[]>(type: "bytea", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    send_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt = table.Column<string>(type: "jsonb", nullable: false),
                    digest = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash_version = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lapses_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency", x => new { x.kind, x.anonymous, x.scope, x.key });
                    table.CheckConstraint("ck_idempotency_hash_version", "hash_version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "messaging",
                columns: table => new
                {
                    name = table.Column<string>(type: "text", nullable: false),
                    claimed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claim_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_renewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.name);
                    table.CheckConstraint("ck_jobs_name", "name IN ('observer', 'balance', 'retention', 'subscriptions', 'watch')");
                });

            migrationBuilder.CreateTable(
                name: "rate_counters",
                schema: "messaging",
                columns: table => new
                {
                    channel = table.Column<string>(type: "text", nullable: false),
                    second = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_counters", x => new { x.channel, x.second });
                    table.CheckConstraint("ck_rate_counters_channel", "channel IN ('email', 'sms', 'push')");
                    table.CheckConstraint("ck_rate_counters_count", "count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "sends",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sends", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    send_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    person = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<string>(type: "text", nullable: false),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    push_topic = table.Column<string>(type: "text", nullable: true),
                    destination = table.Column<byte[]>(type: "bytea", nullable: true),
                    unsubscribe = table.Column<byte[]>(type: "bytea", nullable: true),
                    subject = table.Column<byte[]>(type: "bytea", nullable: true),
                    text = table.Column<byte[]>(type: "bytea", nullable: true),
                    html = table.Column<byte[]>(type: "bytea", nullable: true),
                    title = table.Column<byte[]>(type: "bytea", nullable: true),
                    body = table.Column<byte[]>(type: "bytea", nullable: true),
                    url = table.Column<byte[]>(type: "bytea", nullable: true),
                    tag = table.Column<byte[]>(type: "bytea", nullable: true),
                    icon = table.Column<byte[]>(type: "bytea", nullable: true),
                    badge = table.Column<byte[]>(type: "bytea", nullable: true),
                    content_key_version = table.Column<int>(type: "integer", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    became_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_moved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    first_held_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claimed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claim_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_reference = table.Column<string>(type: "text", nullable: true),
                    next_sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliveries", x => x.id);
                    table.CheckConstraint("ck_deliveries_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_deliveries_channel", "channel IN ('email', 'sms', 'push')");
                    table.CheckConstraint("ck_deliveries_next_sequence", "next_sequence >= 1");
                    table.CheckConstraint("ck_deliveries_reason", "reason IS NULL OR reason IN ('timeout', 'connectionfailed', 'tlsfailed', 'authfailed', 'senderrefused', 'recipientrefused', 'messagerefused', 'badrequest', 'ratelimited', 'temporary', 'permanent', 'gone', 'providerfault', 'keyunavailable', 'consentunavailable', 'reportedfailed', 'claimlost', 'serverrefused', 'destinationnotallowed', 'valuerejected')");
                    table.CheckConstraint("ck_deliveries_status", "status IN ('waiting', 'inflight', 'sent', 'delivered', 'failed', 'cancelled', 'expired', 'suppressed', 'optedout', 'noconsent', 'limited', 'balancefloor', 'nosubscription')");
                    table.ForeignKey(
                        name: "fk_deliveries_sends",
                        column: x => x.send_id,
                        principalSchema: "messaging",
                        principalTable: "sends",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                schema: "messaging",
                columns: table => new
                {
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    @class = table.Column<string>(name: "class", type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempts", x => new { x.delivery_id, x.number });
                    table.CheckConstraint("ck_attempts_class", "class IS NULL OR class IN ('accepted', 'transient', 'permanent', 'gone')");
                    table.CheckConstraint("ck_attempts_number", "number >= 1");
                    table.CheckConstraint("ck_attempts_reason", "reason IS NULL OR reason IN ('timeout', 'connectionfailed', 'tlsfailed', 'authfailed', 'senderrefused', 'recipientrefused', 'messagerefused', 'badrequest', 'ratelimited', 'temporary', 'permanent', 'gone', 'providerfault', 'keyunavailable', 'consentunavailable', 'reportedfailed', 'claimlost', 'serverrefused', 'destinationnotallowed', 'valuerejected')");
                    table.ForeignKey(
                        name: "fk_attempts_deliveries",
                        column: x => x.delivery_id,
                        principalSchema: "messaging",
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notices",
                schema: "messaging",
                columns: table => new
                {
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    claimed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    claim_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notices", x => new { x.delivery_id, x.sequence });
                    table.CheckConstraint("ck_notices_reason", "reason IS NULL OR reason IN ('timeout', 'connectionfailed', 'tlsfailed', 'authfailed', 'senderrefused', 'recipientrefused', 'messagerefused', 'badrequest', 'ratelimited', 'temporary', 'permanent', 'gone', 'providerfault', 'keyunavailable', 'consentunavailable', 'reportedfailed', 'claimlost', 'serverrefused', 'destinationnotallowed', 'valuerejected')");
                    table.CheckConstraint("ck_notices_sequence", "sequence >= 1");
                    table.CheckConstraint("ck_notices_status", "status IN ('waiting', 'inflight', 'sent', 'delivered', 'failed', 'cancelled', 'expired', 'suppressed', 'optedout', 'noconsent', 'limited', 'balancefloor', 'nosubscription')");
                    table.ForeignKey(
                        name: "fk_notices_deliveries",
                        column: x => x.delivery_id,
                        principalSchema: "messaging",
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calls",
                schema: "messaging",
                columns: table => new
                {
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    @class = table.Column<string>(name: "class", type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: true),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    push_service = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calls", x => new { x.delivery_id, x.attempt, x.number });
                    table.CheckConstraint("ck_calls_class", "class IN ('accepted', 'transient', 'permanent', 'gone')");
                    table.CheckConstraint("ck_calls_number", "number >= 1");
                    table.CheckConstraint("ck_calls_push_service", "push_service IS NULL OR push_service IN ('fcm', 'mozilla', 'apple', 'windows', 'other')");
                    table.CheckConstraint("ck_calls_reason", "reason IS NULL OR reason IN ('timeout', 'connectionfailed', 'tlsfailed', 'authfailed', 'senderrefused', 'recipientrefused', 'messagerefused', 'badrequest', 'ratelimited', 'temporary', 'permanent', 'gone', 'providerfault', 'keyunavailable', 'consentunavailable', 'reportedfailed', 'claimlost', 'serverrefused', 'destinationnotallowed', 'valuerejected')");
                    table.ForeignKey(
                        name: "fk_calls_attempts",
                        columns: x => new { x.delivery_id, x.attempt },
                        principalSchema: "messaging",
                        principalTable: "attempts",
                        principalColumns: new[] { "delivery_id", "number" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_content_key_version",
                schema: "messaging",
                table: "deliveries",
                column: "content_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_due",
                schema: "messaging",
                table: "deliveries",
                columns: new[] { "channel", "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_person",
                schema: "messaging",
                table: "deliveries",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_send_id",
                schema: "messaging",
                table: "deliveries",
                column: "send_id");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_lapses_at",
                schema: "messaging",
                table: "idempotency",
                column: "lapses_at");

            migrationBuilder.CreateIndex(
                name: "ix_notices_at",
                schema: "messaging",
                table: "notices",
                column: "at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calls",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "idempotency",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "notices",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "rate_counters",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "attempts",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "deliveries",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "sends",
                schema: "messaging");
        }
    }
}