using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase31_TestAuswertung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bestehensgrenze",
                table: "BewerbungTests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MindestTreffer",
                table: "BewerbungTestFragen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Punkte",
                table: "BewerbungTestFragen",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "RichtigJaNein",
                table: "BewerbungTestFragen",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Schlagwoerter",
                table: "BewerbungTestFragen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "ManuellRichtig",
                table: "BewerbungTestAntworten",
                type: "tinyint(1)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bestehensgrenze",
                table: "BewerbungTests");

            migrationBuilder.DropColumn(
                name: "MindestTreffer",
                table: "BewerbungTestFragen");

            migrationBuilder.DropColumn(
                name: "Punkte",
                table: "BewerbungTestFragen");

            migrationBuilder.DropColumn(
                name: "RichtigJaNein",
                table: "BewerbungTestFragen");

            migrationBuilder.DropColumn(
                name: "Schlagwoerter",
                table: "BewerbungTestFragen");

            migrationBuilder.DropColumn(
                name: "ManuellRichtig",
                table: "BewerbungTestAntworten");
        }
    }
}
