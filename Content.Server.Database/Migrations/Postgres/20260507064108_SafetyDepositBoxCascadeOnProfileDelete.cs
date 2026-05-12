using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class SafetyDepositBoxCascadeOnProfileDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "profile_id",
                table: "wayfarer_safety_deposit_box",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_wayfarer_safety_deposit_box_profile_id",
                table: "wayfarer_safety_deposit_box",
                column: "profile_id");

            // Backfill profile_id for existing rows
            migrationBuilder.Sql("""
                UPDATE wayfarer_safety_deposit_box wsdb
                SET profile_id = p.profile_id
                FROM profile p
                JOIN preference pref ON pref.preference_id = p.preference_id
                WHERE pref.user_id = wsdb.owner_user_id
                  AND p.slot = wsdb.character_index
                  AND wsdb.profile_id IS NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_wayfarer_safety_deposit_box_profile_profile_id",
                table: "wayfarer_safety_deposit_box",
                column: "profile_id",
                principalTable: "profile",
                principalColumn: "profile_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wayfarer_safety_deposit_box_profile_profile_id",
                table: "wayfarer_safety_deposit_box");

            migrationBuilder.DropIndex(
                name: "IX_wayfarer_safety_deposit_box_profile_id",
                table: "wayfarer_safety_deposit_box");

            migrationBuilder.DropColumn(
                name: "profile_id",
                table: "wayfarer_safety_deposit_box");
        }
    }
}
