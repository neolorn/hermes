using System;

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class LimitPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "places",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    window_seconds = table.Column<int>(type: "integer", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    hash_version = table.Column<int>(type: "integer", nullable: true),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lapses_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_places", x => x.id);
                    table.CheckConstraint("ck_places_channel", "channel IN ('email', 'sms', 'push')");
                    table.CheckConstraint("ck_places_hash", "(kind = 'limit') = (hash IS NOT NULL AND hash_version IS NOT NULL)");
                    table.CheckConstraint("ck_places_kind", "kind IN ('limit', 'deployment')");
                    table.CheckConstraint("ck_places_window_seconds", "window_seconds >= 60");
                    table.ForeignKey(
                        name: "fk_places_deliveries",
                        column: x => x.delivery_id,
                        principalSchema: "messaging",
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_places_delivery_id",
                schema: "messaging",
                table: "places",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_places_hash_version",
                schema: "messaging",
                table: "places",
                column: "hash_version");

            migrationBuilder.CreateIndex(
                name: "ix_places_lapses_at",
                schema: "messaging",
                table: "places",
                column: "lapses_at");

            migrationBuilder.CreateIndex(
                name: "ix_places_window",
                schema: "messaging",
                table: "places",
                columns: new[] { "channel", "category", "window_seconds", "kind", "taken_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "places",
                schema: "messaging");
        }
    }
}