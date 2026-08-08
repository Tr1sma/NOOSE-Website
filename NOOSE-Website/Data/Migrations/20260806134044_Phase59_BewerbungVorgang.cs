using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase59_BewerbungVorgang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VorgangId",
                table: "Bewerbungen",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Bewerbungen_VorgangId",
                table: "Bewerbungen",
                column: "VorgangId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bewerbungen_Vorgaenge_VorgangId",
                table: "Bewerbungen",
                column: "VorgangId",
                principalTable: "Vorgaenge",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bewerbungen_Vorgaenge_VorgangId",
                table: "Bewerbungen");

            migrationBuilder.DropIndex(
                name: "IX_Bewerbungen_VorgangId",
                table: "Bewerbungen");

            migrationBuilder.DropColumn(
                name: "VorgangId",
                table: "Bewerbungen");
        }
    }
}
