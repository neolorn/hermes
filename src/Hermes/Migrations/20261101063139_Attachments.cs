using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Attachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_alerts_condition",
                schema: "messaging",
                table: "alerts");

            migrationBuilder.CreateTable(
                name: "attachments",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    named_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    uploader = table.Column<string>(type: "text", nullable: true),
                    visitor = table.Column<byte[]>(type: "bytea", nullable: true),
                    hash_version = table.Column<int>(type: "integer", nullable: true),
                    owner = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<byte[]>(type: "bytea", nullable: true),
                    content_key_version = table.Column<int>(type: "integer", nullable: true),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    chunks = table.Column<int>(type: "integer", nullable: false),
                    store_key = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    scan_attempts = table.Column<int>(type: "integer", nullable: false),
                    scan_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachments_size", "size >= 0 AND chunks >= 0 AND scan_attempts >= 0");
                    table.CheckConstraint("ck_attachments_status", "status IN ('pending', 'clean', 'infected', 'failed')");
                    table.CheckConstraint("ck_attachments_uploader", "uploader IS NULL OR visitor IS NULL");
                    table.ForeignKey(
                        name: "fk_attachments_conversations",
                        column: x => x.conversation_id,
                        principalSchema: "messaging",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_attachments_messages",
                        column: x => x.message_id,
                        principalSchema: "messaging",
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs",
                sql: "name IN ('observer', 'balance', 'retention', 'subscriptions', 'watch', 'conversation-timers', 'attachment-scans')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_alerts_condition",
                schema: "messaging",
                table: "alerts",
                sql: "condition IN ('email-submission-failing', 'sms-balance', 'sms-balance-unreadable', 'sms-report-rejected', 'push-auth-failing', 'dispatch-lag', 'job-stalled', 'newer-release', 'configuration-invalid', 'content-key-limit', 'key-unavailable', 'content-rejected', 'observer-failing', 'attachment-scan')");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_content_key_version",
                schema: "messaging",
                table: "attachments",
                column: "content_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_conversation_id",
                schema: "messaging",
                table: "attachments",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_created_at",
                schema: "messaging",
                table: "attachments",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_message_id",
                schema: "messaging",
                table: "attachments",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_owner",
                schema: "messaging",
                table: "attachments",
                column: "owner");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_scan_at",
                schema: "messaging",
                table: "attachments",
                column: "scan_at");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_uploader",
                schema: "messaging",
                table: "attachments",
                column: "uploader");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_alerts_condition",
                schema: "messaging",
                table: "alerts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs",
                sql: "name IN ('observer', 'balance', 'retention', 'subscriptions', 'watch', 'conversation-timers')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_alerts_condition",
                schema: "messaging",
                table: "alerts",
                sql: "condition IN ('email-submission-failing', 'sms-balance', 'sms-balance-unreadable', 'sms-report-rejected', 'push-auth-failing', 'dispatch-lag', 'job-stalled', 'newer-release', 'configuration-invalid', 'content-key-limit', 'key-unavailable', 'content-rejected', 'observer-failing')");
        }
    }
}