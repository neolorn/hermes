using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class SupportSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "person",
                schema: "messaging",
                table: "participants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "hash_version",
                schema: "messaging",
                table: "participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "visitor",
                schema: "messaging",
                table: "participants",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner",
                schema: "messaging",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE messaging.messages SET owner = author;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assigned_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assignee",
                schema: "messaging",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_by",
                schema: "messaging",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer",
                schema: "messaging",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "email",
                schema: "messaging",
                table: "conversations",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "email_key_version",
                schema: "messaging",
                table: "conversations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "offline",
                schema: "messaging",
                table: "conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "queued_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "topic",
                schema: "messaging",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "support_acknowledgements",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    visitor = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash_version = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_acknowledgements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "support_agents",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false),
                    available = table.Column<bool>(type: "boolean", nullable: false),
                    last_assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "support_blocks",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: true),
                    visitor = table.Column<byte[]>(type: "bytea", nullable: true),
                    hash_version = table.Column<int>(type: "integer", nullable: true),
                    until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    blocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_blocks", x => x.id);
                    table.CheckConstraint("ck_support_blocks_who", "(person IS NULL) <> (visitor IS NULL) AND (visitor IS NULL) = (hash_version IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "support_counters",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    hash_version = table.Column<int>(type: "integer", nullable: false),
                    window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_counters", x => x.id);
                    table.CheckConstraint("ck_support_counters_kind", "kind IN ('visitor-starts', 'visitor-messages', 'source-starts')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_participants_hash_version",
                schema: "messaging",
                table: "participants",
                column: "hash_version");

            migrationBuilder.CreateIndex(
                name: "ix_participants_visitor",
                schema: "messaging",
                table: "participants",
                column: "visitor");

            migrationBuilder.CreateIndex(
                name: "ux_participants_conversation_visitor",
                schema: "messaging",
                table: "participants",
                columns: new[] { "conversation_id", "visitor" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_participants_who",
                schema: "messaging",
                table: "participants",
                sql: "(person IS NULL) <> (visitor IS NULL) AND (visitor IS NULL) = (hash_version IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_messages_owner",
                schema: "messaging",
                table: "messages",
                column: "owner");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_assignee",
                schema: "messaging",
                table: "conversations",
                column: "assignee");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_closed_by",
                schema: "messaging",
                table: "conversations",
                column: "closed_by");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_customer",
                schema: "messaging",
                table: "conversations",
                column: "customer");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_email_key_version",
                schema: "messaging",
                table: "conversations",
                column: "email_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_topic_queued_at",
                schema: "messaging",
                table: "conversations",
                columns: new[] { "topic", "queued_at", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_conversations_email",
                schema: "messaging",
                table: "conversations",
                sql: "(email IS NULL) OR offline");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conversations_topic",
                schema: "messaging",
                table: "conversations",
                sql: "(type = 'support') = (topic IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_support_acknowledgements_hash_version",
                schema: "messaging",
                table: "support_acknowledgements",
                column: "hash_version");

            migrationBuilder.CreateIndex(
                name: "ix_support_acknowledgements_visitor_version",
                schema: "messaging",
                table: "support_acknowledgements",
                columns: new[] { "visitor", "version" });

            migrationBuilder.CreateIndex(
                name: "ux_support_agents_person",
                schema: "messaging",
                table: "support_agents",
                column: "person",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_support_blocks_hash_version",
                schema: "messaging",
                table: "support_blocks",
                column: "hash_version");

            migrationBuilder.CreateIndex(
                name: "ix_support_blocks_person",
                schema: "messaging",
                table: "support_blocks",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "ix_support_blocks_visitor",
                schema: "messaging",
                table: "support_blocks",
                column: "visitor");

            migrationBuilder.CreateIndex(
                name: "ix_support_counters_hash_version",
                schema: "messaging",
                table: "support_counters",
                column: "hash_version");

            migrationBuilder.CreateIndex(
                name: "ix_support_counters_window_start",
                schema: "messaging",
                table: "support_counters",
                column: "window_start");

            migrationBuilder.CreateIndex(
                name: "ux_support_counters_kind_hash_window",
                schema: "messaging",
                table: "support_counters",
                columns: new[] { "kind", "hash", "window_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_acknowledgements",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "support_agents",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "support_blocks",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "support_counters",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "ix_participants_hash_version",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "ix_participants_visitor",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "ux_participants_conversation_visitor",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_participants_who",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "ix_messages_owner",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ix_conversations_assignee",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_closed_by",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_customer",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_email_key_version",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_topic_queued_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conversations_email",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conversations_topic",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "hash_version",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "visitor",
                schema: "messaging",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "owner",
                schema: "messaging",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "assignee",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "customer",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "email_key_version",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "offline",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "queued_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "topic",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.AlterColumn<string>(
                name: "person",
                schema: "messaging",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}