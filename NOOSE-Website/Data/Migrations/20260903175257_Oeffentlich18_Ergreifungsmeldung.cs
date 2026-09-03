using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich18_Ergreifungsmeldung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Art",
                table: "Hinweise",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Uebergabe",
                table: "Hinweise",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uebergabeort",
                table: "Hinweise",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Art",
                table: "Hinweise");

            migrationBuilder.DropColumn(
                name: "Uebergabe",
                table: "Hinweise");

            migrationBuilder.DropColumn(
                name: "Uebergabeort",
                table: "Hinweise");
        }
    }
}
