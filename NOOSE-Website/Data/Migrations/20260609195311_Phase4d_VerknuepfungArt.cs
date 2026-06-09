using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4d_VerknuepfungArt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Art",
                table: "Verknuepfungen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: bisherige Fraktion↔Fraktion-Verknüpfungen waren ausnahmslos Konflikte (Art = 1).
            // Person↔Person-Links (manuell + automatische „Kollege") bleiben Standard (0).
            migrationBuilder.Sql("UPDATE `Verknuepfungen` SET `Art` = 1 WHERE `VonTyp` = 'Fraktion' AND `NachTyp` = 'Fraktion';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Art",
                table: "Verknuepfungen");
        }
    }
}
