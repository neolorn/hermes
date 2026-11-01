using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Conversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    scope_kind = table.Column<string>(type: "text", nullable: true),
                    scope_reference = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.CheckConstraint("ck_conversations_origin", "origin IN ('web')");
                    table.CheckConstraint("ck_conversations_scope", "(type = 'group') = (scope_kind IS NOT NULL) AND (scope_kind IS NULL) = (scope_reference IS NULL)");
                    table.CheckConstraint("ck_conversations_scope_kind", "scope_kind IN ('organization', 'group')");
                    table.CheckConstraint("ck_conversations_status", "status IN ('open', 'snoozed', 'closed')");
                    table.CheckConstraint("ck_conversations_type", "type IN ('support', 'group', 'direct')");
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_kind = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<byte[]>(type: "bytea", nullable: true),
                    content_key_version = table.Column<int>(type: "integer", nullable: true),
                    reply_to = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.CheckConstraint("ck_messages_author_kind", "author_kind IN ('person', 'visitor', 'library')");
                    table.CheckConstraint("ck_messages_type", "type IN ('text', 'note', 'automatic', 'system')");
                    table.ForeignKey(
                        name: "fk_messages_conversations",
                        column: x => x.conversation_id,
                        principalSchema: "messaging",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messages_messages",
                        column: x => x.reply_to,
                        principalSchema: "messaging",
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mentions",
                schema: "messaging",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mentions", x => new { x.message_id, x.person });
                    table.ForeignKey(
                        name: "fk_mentions_messages",
                        column: x => x.message_id,
                        principalSchema: "messaging",
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false),
                    read_position = table.Column<Guid>(type: "uuid", nullable: true),
                    muted = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_participants_conversations",
                        column: x => x.conversation_id,
                        principalSchema: "messaging",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_participants_messages",
                        column: x => x.read_position,
                        principalSchema: "messaging",
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ux_conversations_scope",
                schema: "messaging",
                table: "conversations",
                columns: new[] { "scope_kind", "scope_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mentions_person",
                schema: "messaging",
                table: "mentions",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "ix_messages_author",
                schema: "messaging",
                table: "messages",
                column: "author");

            migrationBuilder.CreateIndex(
                name: "ix_messages_content_key_version",
                schema: "messaging",
                table: "messages",
                column: "content_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_sent_at",
                schema: "messaging",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_reply_to",
                schema: "messaging",
                table: "messages",
                column: "reply_to");

            migrationBuilder.CreateIndex(
                name: "ix_participants_person",
                schema: "messaging",
                table: "participants",
                column: "person");

            migrationBuilder.CreateIndex(
                name: "ix_participants_read_position",
                schema: "messaging",
                table: "participants",
                column: "read_position");

            migrationBuilder.CreateIndex(
                name: "ux_participants_conversation_person",
                schema: "messaging",
                table: "participants",
                columns: new[] { "conversation_id", "person" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "participants",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "messaging");
        }
    }
}