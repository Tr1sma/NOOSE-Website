using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase22_DokumentVerschlussTRUHRB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Zusätzliche Verschluss-Stufen für Bibliotheks-Dokumente und -Dateien: „nur für TRU" und
            // „nur für HRB" – neben der bestehenden „nur für Führung" (IstVerschlusssache).
            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Dokumente",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Dokumente",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "BibliothekDateien",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "BibliothekDateien",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Dokumente");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Dokumente");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "BibliothekDateien");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "BibliothekDateien");
        }
    }
}
