using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich08_HinweisTriage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_DublettenGruppeId",
                table: "Hinweise",
                column: "DublettenGruppeId");

            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_Status_Prioritaet",
                table: "Hinweise",
                columns: new[] { "Status", "Prioritaet" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hinweise_DublettenGruppeId",
                table: "Hinweise");

            migrationBuilder.DropIndex(
                name: "IX_Hinweise_Status_Prioritaet",
                table: "Hinweise");
        }
    }
}
