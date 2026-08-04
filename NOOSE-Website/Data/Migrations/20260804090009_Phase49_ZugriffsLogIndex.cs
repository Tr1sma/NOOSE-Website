using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase49_ZugriffsLogIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ZugriffsLogs_AgentId_Zeitpunkt",
                table: "ZugriffsLogs",
                columns: new[] { "AgentId", "Zeitpunkt" });

            migrationBuilder.CreateIndex(
                name: "IX_ZugriffsLogs_Zeitpunkt",
                table: "ZugriffsLogs",
                column: "Zeitpunkt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZugriffsLogs_AgentId_Zeitpunkt",
                table: "ZugriffsLogs");

            migrationBuilder.DropIndex(
                name: "IX_ZugriffsLogs_Zeitpunkt",
                table: "ZugriffsLogs");
        }
    }
}
