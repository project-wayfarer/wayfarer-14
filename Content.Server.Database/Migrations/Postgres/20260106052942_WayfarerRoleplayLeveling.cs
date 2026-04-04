using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class WayfarerRoleplayLeveling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wayfarer_roleplay_commends",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    round_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_profile_id = table.Column<int>(type: "integer", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    giver_profile_id = table.Column<int>(type: "integer", nullable: false),
                    giver_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    is_private = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_roleplay_commends", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wayfarer_roleplay_levels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    experience = table.Column<long>(type: "bigint", nullable: false),
                    experience_to_next_level = table.Column<long>(type: "bigint", nullable: false),
                    total_commends = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_roleplay_levels", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_roleplay_commends_giver_user_id",
                table: "wayfarer_roleplay_commends",
                column: "giver_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_roleplay_commends_recipient_user_id",
                table: "wayfarer_roleplay_commends",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_roleplay_commends_round_id",
                table: "wayfarer_roleplay_commends",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_roleplay_levels_level",
                table: "wayfarer_roleplay_levels",
                column: "level");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_roleplay_levels_user_id",
                table: "wayfarer_roleplay_levels",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wayfarer_roleplay_commends");

            migrationBuilder.DropTable(
                name: "wayfarer_roleplay_levels");
        }
    }
}
