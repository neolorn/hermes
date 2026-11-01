using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class DeskTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "close_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closing_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language",
                schema: "messaging",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "replies_fired",
                schema: "messaging",
                table: "conversations",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "snoozed_until",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                schema: "messaging",
                table: "conversations",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "waiting_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "support_replies",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: true),
                    person = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_replies", x => x.id);
                    table.CheckConstraint("ck_support_replies_owner", "(topic IS NULL) <> (person IS NULL)");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs",
                sql: "name IN ('observer', 'balance', 'retention', 'subscriptions', 'watch', 'conversation-timers')");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_close_at",
                schema: "messaging",
                table: "conversations",
                column: "close_at");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_closing_at",
                schema: "messaging",
                table: "conversations",
                column: "closing_at");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_snoozed_until",
                schema: "messaging",
                table: "conversations",
                column: "snoozed_until");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_waiting_at",
                schema: "messaging",
                table: "conversations",
                column: "waiting_at");

            migrationBuilder.CreateIndex(
                name: "ux_support_replies_person_name",
                schema: "messaging",
                table: "support_replies",
                columns: new[] { "person", "name" },
                unique: true,
                filter: "person IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_support_replies_topic_name",
                schema: "messaging",
                table: "support_replies",
                columns: new[] { "topic", "name" },
                unique: true,
                filter: "topic IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_replies",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "ix_conversations_close_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_closing_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_snoozed_until",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_waiting_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "close_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "closing_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "language",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "replies_fired",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "snoozed_until",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "tags",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "waiting_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_name",
                schema: "messaging",
                table: "jobs",
                sql: "name IN ('observer', 'balance', 'retention', 'subscriptions', 'watch')");
        }
    }
}