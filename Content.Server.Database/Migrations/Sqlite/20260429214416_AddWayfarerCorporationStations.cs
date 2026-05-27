using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddWayfarerCorporationStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wayfarer_corporation_stations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    corporation_id = table.Column<int>(type: "INTEGER", nullable: false),
                    station_name = table.Column<string>(type: "TEXT", nullable: false),
                    save_path = table.Column<string>(type: "TEXT", nullable: false),
                    purchased_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_corporation_stations", x => x.id);
                    table.ForeignKey(
                        name: "FK_wayfarer_corporation_stations_wayfarer_corporations_corporation_id",
                        column: x => x.corporation_id,
                        principalTable: "wayfarer_corporations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_corporation_stations_corporation_id",
                table: "wayfarer_corporation_stations",
                column: "corporation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wayfarer_corporation_stations");
        }
    }
}
