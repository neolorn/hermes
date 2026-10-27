using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hermes.Migrations
{
    /// <inheritdoc />
    internal sealed partial class BalanceReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balance_readings",
                schema: "messaging",
                columns: table => new
                {
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    units = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balance_readings", x => x.read_at);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balance_readings",
                schema: "messaging");
        }
    }
}