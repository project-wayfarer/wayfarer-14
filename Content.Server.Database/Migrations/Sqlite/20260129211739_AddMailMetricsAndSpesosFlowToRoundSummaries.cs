using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddMailMetricsAndSpesosFlowToRoundSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "mail_metrics_data",
                table: "wayfarer_round_summaries",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "spesos_flow_data",
                table: "wayfarer_round_summaries",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mail_metrics_data",
                table: "wayfarer_round_summaries");

            migrationBuilder.DropColumn(
                name: "spesos_flow_data",
                table: "wayfarer_round_summaries");
        }
    }
}
