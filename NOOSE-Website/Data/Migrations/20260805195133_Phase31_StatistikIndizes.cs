using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase31_StatistikIndizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Vorgaenge_AbgeschlossenAm",
                table: "Vorgaenge",
                column: "AbgeschlossenAm");

            migrationBuilder.CreateIndex(
                name: "IX_Personen_ErstelltAm",
                table: "Personen",
                column: "ErstelltAm");

            migrationBuilder.CreateIndex(
                name: "IX_PersonDoks_Zeitpunkt",
                table: "PersonDoks",
                column: "Zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "IX_EinstufungVerlauf_EntitaetTyp_Zeitpunkt",
                table: "EinstufungVerlauf",
                columns: new[] { "EntitaetTyp", "Zeitpunkt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vorgaenge_AbgeschlossenAm",
                table: "Vorgaenge");

            migrationBuilder.DropIndex(
                name: "IX_Personen_ErstelltAm",
                table: "Personen");

            migrationBuilder.DropIndex(
                name: "IX_PersonDoks_Zeitpunkt",
                table: "PersonDoks");

            migrationBuilder.DropIndex(
                name: "IX_EinstufungVerlauf_EntitaetTyp_Zeitpunkt",
                table: "EinstufungVerlauf");
        }
    }
}
