using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase12_AufgabeEingeschraenkt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IstEingeschraenkt",
                table: "Aufgaben",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Aufgaben_IstEingeschraenkt",
                table: "Aufgaben",
                column: "IstEingeschraenkt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Aufgaben_IstEingeschraenkt",
                table: "Aufgaben");

            migrationBuilder.DropColumn(
                name: "IstEingeschraenkt",
                table: "Aufgaben");
        }
    }
}
