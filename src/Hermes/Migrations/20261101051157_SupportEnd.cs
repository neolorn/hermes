using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class SupportEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "survey_at",
                schema: "messaging",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "survey_comment",
                schema: "messaging",
                table: "conversations",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "survey_comment_key_version",
                schema: "messaging",
                table: "conversations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "survey_person",
                schema: "messaging",
                table: "conversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "survey_rating",
                schema: "messaging",
                table: "conversations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "survey_resolved",
                schema: "messaging",
                table: "conversations",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "transcript_sends",
                schema: "messaging",
                table: "conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_survey_comment_key_version",
                schema: "messaging",
                table: "conversations",
                column: "survey_comment_key_version");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_survey_person",
                schema: "messaging",
                table: "conversations",
                column: "survey_person");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_topic_closed_at",
                schema: "messaging",
                table: "conversations",
                columns: new[] { "topic", "closed_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_conversations_survey",
                schema: "messaging",
                table: "conversations",
                sql: "(survey_at IS NULL) = (survey_rating IS NULL) AND (survey_at IS NULL) = (survey_resolved IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conversations_survey_rating",
                schema: "messaging",
                table: "conversations",
                sql: "survey_rating IS NULL OR survey_rating BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conversations_transcript_sends",
                schema: "messaging",
                table: "conversations",
                sql: "transcript_sends BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_conversations_survey_comment_key_version",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_survey_person",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "ix_conversations_topic_closed_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conversations_survey",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conversations_survey_rating",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conversations_transcript_sends",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "survey_at",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "survey_comment",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "survey_comment_key_version",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "survey_person",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "survey_rating",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "survey_resolved",
                schema: "messaging",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "transcript_sends",
                schema: "messaging",
                table: "conversations");
        }
    }
}