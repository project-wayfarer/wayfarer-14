using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddWayfarerCorporations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wayfarer_corporations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    privacy = table.Column<int>(type: "INTEGER", nullable: false),
                    balance = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_corporations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wayfarer_corporation_invites",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    corporation_id = table.Column<int>(type: "INTEGER", nullable: false),
                    invitee_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sent_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_corporation_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_wayfarer_corporation_invites_wayfarer_corporations_corporation_id",
                        column: x => x.corporation_id,
                        principalTable: "wayfarer_corporations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wayfarer_corporation_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    corporation_id = table.Column<int>(type: "INTEGER", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    rank = table.Column<int>(type: "INTEGER", nullable: false),
                    joined_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wayfarer_corporation_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_wayfarer_corporation_members_wayfarer_corporations_corporation_id",
                        column: x => x.corporation_id,
                        principalTable: "wayfarer_corporations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_corporation_invites_corporation_id",
                table: "wayfarer_corporation_invites",
                column: "corporation_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_corporation_invites_invitee_user_id",
                table: "wayfarer_corporation_invites",
                column: "invitee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_corporation_members_corporation_id",
                table: "wayfarer_corporation_members",
                column: "corporation_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_corporation_members_user_id",
                table: "wayfarer_corporation_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_corporations_name",
                table: "wayfarer_corporations",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wayfarer_corporation_invites");

            migrationBuilder.DropTable(
                name: "wayfarer_corporation_members");

            migrationBuilder.DropTable(
                name: "wayfarer_corporations");
        }
    }
}
