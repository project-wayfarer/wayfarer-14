using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddWayfarerCommunityGoalContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wayfarer_community_goal_contributions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requirement_id = table.Column<int>(type: "integer", nullable: false),
                    player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_name = table.Column<string>(type: "text", nullable: false),
                    entity_prototype_id = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    round_id = table.Column<int>(type: "integer", nullable: false),
                    contributed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_community_goal_contributions", x => x.id);
                    table.ForeignKey(
                        name: "FK_wayfarer_community_goal_contributions_wayfarer_community_go~",
                        column: x => x.requirement_id,
                        principalTable: "wayfarer_community_goal_requirements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_community_goal_contributions_player_user_id",
                table: "wayfarer_community_goal_contributions",
                column: "player_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_community_goal_contributions_requirement_id",
                table: "wayfarer_community_goal_contributions",
                column: "requirement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wayfarer_community_goal_contributions");
        }
    }
}
