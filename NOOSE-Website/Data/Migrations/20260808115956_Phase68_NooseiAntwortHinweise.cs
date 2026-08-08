using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase68_NooseiAntwortHinweise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Gekuerzt",
                table: "KiNachrichten",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NichtBelegt",
                table: "KiNachrichten",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "OhneAktenzugriff",
                table: "KiNachrichten",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gekuerzt",
                table: "KiNachrichten");

            migrationBuilder.DropColumn(
                name: "NichtBelegt",
                table: "KiNachrichten");

            migrationBuilder.DropColumn(
                name: "OhneAktenzugriff",
                table: "KiNachrichten");
        }
    }
}
