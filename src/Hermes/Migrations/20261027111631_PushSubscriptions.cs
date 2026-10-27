using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class PushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "push_auth_failures",
                schema: "messaging",
                columns: table => new
                {
                    service = table.Column<string>(type: "text", nullable: false),
                    minute = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_auth_failures", x => new { x.service, x.minute });
                    table.CheckConstraint("ck_push_auth_failures_count", "count >= 1");
                    table.CheckConstraint("ck_push_auth_failures_service", "service IN ('fcm', 'mozilla', 'apple', 'windows', 'other')");
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false),
                    endpoint_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash_version = table.Column<int>(type: "integer", nullable: false),
                    endpoint = table.Column<byte[]>(type: "bytea", nullable: false),
                    keys = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_key_version = table.Column<int>(type: "integer", nullable: true),
                    vapid_version = table.Column<int>(type: "integer", nullable: false),
                    service = table.Column<string>(type: "text", nullable: false),
                    made_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.CheckConstraint("ck_subscriptions_service", "service IN ('fcm', 'mozilla', 'apple', 'windows', 'other')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_calls_subscription_id",
                schema: "messaging",
                table: "calls",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_content_key_version",
                schema: "messaging",
                table: "subscriptions",
                column: "content_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_expires_at",
                schema: "messaging",
                table: "subscriptions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_person",
                schema: "messaging",
                table: "subscriptions",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_vapid_version",
                schema: "messaging",
                table: "subscriptions",
                column: "vapid_version");

            migrationBuilder.CreateIndex(
                name: "ux_subscriptions_endpoint_hash",
                schema: "messaging",
                table: "subscriptions",
                column: "endpoint_hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_calls_subscriptions",
                schema: "messaging",
                table: "calls",
                column: "subscription_id",
                principalSchema: "messaging",
                principalTable: "subscriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_calls_subscriptions",
                schema: "messaging",
                table: "calls");

            migrationBuilder.DropTable(
                name: "push_auth_failures",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "ix_calls_subscription_id",
                schema: "messaging",
                table: "calls");
        }
    }
}