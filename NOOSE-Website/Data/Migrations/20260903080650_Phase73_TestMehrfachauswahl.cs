using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase73_TestMehrfachauswahl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MehrfachauswahlErlaubt",
                table: "BewerbungTestFragen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OptionIds",
                table: "BewerbungTestAntworten",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MehrfachauswahlErlaubt",
                table: "BewerbungTestFragen");

            migrationBuilder.DropColumn(
                name: "OptionIds",
                table: "BewerbungTestAntworten");
        }
    }
}
