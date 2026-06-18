using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase29_DokumentTaskforceIntern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BesitzerTaskforceId",
                table: "Dokumente",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Dokumente_BesitzerTaskforceId",
                table: "Dokumente",
                column: "BesitzerTaskforceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dokumente_BesitzerTaskforceId",
                table: "Dokumente");

            migrationBuilder.DropColumn(
                name: "BesitzerTaskforceId",
                table: "Dokumente");
        }
    }
}
