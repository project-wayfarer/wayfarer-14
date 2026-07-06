using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
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
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    corporation_id = table.Column<int>(type: "integer", nullable: false),
                    station_name = table.Column<string>(type: "text", nullable: false),
                    save_path = table.Column<string>(type: "text", nullable: false),
                    purchased_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_corporation_stations", x => x.id);
                    table.ForeignKey(
                        name: "FK_wayfarer_corporation_stations_wayfarer_corporations_corpora~",
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
