using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase69_KiAnfragenBetrieb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbbruchGrund",
                table: "KiAnfragen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Abschlussgrund",
                table: "KiAnfragen",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Eingeschraenkt",
                table: "KiAnfragen",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Fehlerart",
                table: "KiAnfragen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModellDauerMs",
                table: "KiAnfragen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Versuche",
                table: "KiAnfragen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Werkzeugaufrufe",
                table: "KiAnfragen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Werkzeugfehler",
                table: "KiAnfragen",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbbruchGrund",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "Abschlussgrund",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "Eingeschraenkt",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "Fehlerart",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "ModellDauerMs",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "Versuche",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "Werkzeugaufrufe",
                table: "KiAnfragen");

            migrationBuilder.DropColumn(
                name: "Werkzeugfehler",
                table: "KiAnfragen");
        }
    }
}
