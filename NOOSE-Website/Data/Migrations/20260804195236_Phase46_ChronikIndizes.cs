using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase46_ChronikIndizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BedrohungsScoreVerlauf_Zeitpunkt",
                table: "BedrohungsScoreVerlauf",
                column: "Zeitpunkt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BedrohungsScoreVerlauf_Zeitpunkt",
                table: "BedrohungsScoreVerlauf");
        }
    }
}
