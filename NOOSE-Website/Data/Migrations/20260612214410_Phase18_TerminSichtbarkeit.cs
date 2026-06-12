using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase18_TerminSichtbarkeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Neue Stufen-Spalte zuerst anlegen (Default Oeffentlich = 0) …
            migrationBuilder.AddColumn<int>(
                name: "Sichtbarkeit",
                table: "Termine",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // … dann die Altdaten kopieren: eingeschränkte Termine bleiben „Eingeschränkt" (= 1), Rest „Öffentlich" (= 0).
            migrationBuilder.Sql("UPDATE Termine SET Sichtbarkeit = 1 WHERE IstEingeschraenkt = 1;");

            // … erst danach die alte Spalte + Index entfernen.
            migrationBuilder.DropIndex(
                name: "IX_Termine_IstEingeschraenkt",
                table: "Termine");

            migrationBuilder.DropColumn(
                name: "IstEingeschraenkt",
                table: "Termine");

            migrationBuilder.CreateIndex(
                name: "IX_Termine_Sichtbarkeit",
                table: "Termine",
                column: "Sichtbarkeit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IstEingeschraenkt",
                table: "Termine",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Beim Zurückrollen werden Eingeschränkt (1) UND Privat (2) zu „eingeschränkt" (nächstliegendes Verhalten).
            migrationBuilder.Sql("UPDATE Termine SET IstEingeschraenkt = 1 WHERE Sichtbarkeit <> 0;");

            migrationBuilder.DropIndex(
                name: "IX_Termine_Sichtbarkeit",
                table: "Termine");

            migrationBuilder.DropColumn(
                name: "Sichtbarkeit",
                table: "Termine");

            migrationBuilder.CreateIndex(
                name: "IX_Termine_IstEingeschraenkt",
                table: "Termine",
                column: "IstEingeschraenkt");
        }
    }
}
