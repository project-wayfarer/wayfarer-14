using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddWayfarerCommunityGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wayfarer_community_goals",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    start_round = table.Column<int>(type: "integer", nullable: true),
                    end_round = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_community_goals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wayfarer_community_goal_requirements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    goal_id = table.Column<int>(type: "integer", nullable: false),
                    entity_prototype_id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    required_amount = table.Column<long>(type: "bigint", nullable: false),
                    current_amount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_community_goal_requirements", x => x.id);
                    table.ForeignKey(
                        name: "FK_wayfarer_community_goal_requirements_wayfarer_community_goa~",
                        column: x => x.goal_id,
                        principalTable: "wayfarer_community_goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_community_goal_requirements_goal_id",
                table: "wayfarer_community_goal_requirements",
                column: "goal_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_community_goals_is_active",
                table: "wayfarer_community_goals",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wayfarer_community_goal_requirements");

            migrationBuilder.DropTable(
                name: "wayfarer_community_goals");
        }
    }
}
