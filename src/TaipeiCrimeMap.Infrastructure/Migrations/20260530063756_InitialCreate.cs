using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaipeiCrimeMap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "theft_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    case_type = table.Column<int>(type: "integer", nullable: true),
                    district = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    occurred_date_raw = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    occurred_date = table.Column<DateOnly>(type: "date", nullable: true),
                    occurred_year = table.Column<int>(type: "integer", nullable: true),
                    time_slot_raw = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    time_slot_start = table.Column<int>(type: "integer", nullable: true),
                    time_slot_end = table.Column<int>(type: "integer", nullable: true),
                    raw_location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_theft_cases", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_theft_cases_case_number",
                table: "theft_cases",
                column: "case_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "theft_cases");
        }
    }
}
