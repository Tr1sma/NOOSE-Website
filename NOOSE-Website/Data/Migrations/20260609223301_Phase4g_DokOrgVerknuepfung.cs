using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4g_DokOrgVerknuepfung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrgId",
                table: "PersonDoks",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OrgTyp",
                table: "PersonDoks",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PersonDoks_OrgTyp_OrgId",
                table: "PersonDoks",
                columns: new[] { "OrgTyp", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonDoks_OrgTyp_OrgId",
                table: "PersonDoks");

            migrationBuilder.DropColumn(
                name: "OrgId",
                table: "PersonDoks");

            migrationBuilder.DropColumn(
                name: "OrgTyp",
                table: "PersonDoks");
        }
    }
}
