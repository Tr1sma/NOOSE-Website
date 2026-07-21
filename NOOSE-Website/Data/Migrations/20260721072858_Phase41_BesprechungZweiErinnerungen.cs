using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase41_BesprechungZweiErinnerungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Besprechungen_Beginn_Status_ErinnerungGesendetAm",
                table: "Besprechungen");

            migrationBuilder.RenameColumn(
                name: "ErinnerungGesendetAm",
                table: "Besprechungen",
                newName: "Erinnerung30MinGesendetAm");

            migrationBuilder.AddColumn<DateTime>(
                name: "Erinnerung1TagGesendetAm",
                table: "Besprechungen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_Beginn_Status",
                table: "Besprechungen",
                columns: new[] { "Beginn", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Besprechungen_Beginn_Status",
                table: "Besprechungen");

            migrationBuilder.DropColumn(
                name: "Erinnerung1TagGesendetAm",
                table: "Besprechungen");

            migrationBuilder.RenameColumn(
                name: "Erinnerung30MinGesendetAm",
                table: "Besprechungen",
                newName: "ErinnerungGesendetAm");

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_Beginn_Status_ErinnerungGesendetAm",
                table: "Besprechungen",
                columns: new[] { "Beginn", "Status", "ErinnerungGesendetAm" });
        }
    }
}
