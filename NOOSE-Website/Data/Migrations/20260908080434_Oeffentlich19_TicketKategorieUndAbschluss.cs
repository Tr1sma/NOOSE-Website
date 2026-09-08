using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich19_TicketKategorieUndAbschluss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Abschlussgrund",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Abschlussnotiz",
                table: "Tickets",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Kategorie",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Kategorie_Status_LetzteAktivitaetAm",
                table: "Tickets",
                columns: new[] { "Kategorie", "Status", "LetzteAktivitaetAm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_Kategorie_Status_LetzteAktivitaetAm",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Abschlussgrund",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Abschlussnotiz",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Kategorie",
                table: "Tickets");
        }
    }
}
