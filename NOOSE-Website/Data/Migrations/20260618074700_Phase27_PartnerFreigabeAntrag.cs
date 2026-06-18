using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase27_PartnerFreigabeAntrag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreigabeBehoerde",
                table: "Antraege",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FreigabeInklusiveKinder",
                table: "Antraege",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FreigabePartnerAgentId",
                table: "Antraege",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreigabeBehoerde",
                table: "Antraege");

            migrationBuilder.DropColumn(
                name: "FreigabeInklusiveKinder",
                table: "Antraege");

            migrationBuilder.DropColumn(
                name: "FreigabePartnerAgentId",
                table: "Antraege");
        }
    }
}
