using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase84_BesuchsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ZugriffsLogs_AgentId_EntitaetTyp_EntitaetId_Zeitpunkt",
                table: "ZugriffsLogs",
                columns: new[] { "AgentId", "EntitaetTyp", "EntitaetId", "Zeitpunkt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ZugriffsLogs_AgentId_EntitaetTyp_EntitaetId_Zeitpunkt",
                table: "ZugriffsLogs");
        }
    }
}
