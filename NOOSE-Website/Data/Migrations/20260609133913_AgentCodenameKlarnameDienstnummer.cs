using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgentCodenameKlarnameDienstnummer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Anzeigename",
                table: "AspNetUsers",
                newName: "Codename");

            migrationBuilder.AddColumn<string>(
                name: "Dienstnummer",
                table: "AspNetUsers",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Klarname",
                table: "AspNetUsers",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Dienstnummer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Klarname",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "Codename",
                table: "AspNetUsers",
                newName: "Anzeigename");
        }
    }
}
