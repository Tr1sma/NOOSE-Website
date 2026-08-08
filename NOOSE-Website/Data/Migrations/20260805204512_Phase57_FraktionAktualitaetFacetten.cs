using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase57_FraktionAktualitaetFacetten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AktivitaetenAktualisiertAm",
                table: "Fraktionen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BestaendeAktualisiertAm",
                table: "Fraktionen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DoksAktualisiertAm",
                table: "Fraktionen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MitgliederAktualisiertAm",
                table: "Fraktionen",
                type: "datetime(6)",
                nullable: true);

            // Seed every facet with the record's last change, so existing factions keep their current light
            // instead of all turning red at once.
            migrationBuilder.Sql("""
                UPDATE Fraktionen SET
                    MitgliederAktualisiertAm = COALESCE(GeaendertAm, ErstelltAm),
                    BestaendeAktualisiertAm = COALESCE(GeaendertAm, ErstelltAm),
                    AktivitaetenAktualisiertAm = COALESCE(GeaendertAm, ErstelltAm),
                    DoksAktualisiertAm = COALESCE(GeaendertAm, ErstelltAm)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AktivitaetenAktualisiertAm",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "BestaendeAktualisiertAm",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "DoksAktualisiertAm",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "MitgliederAktualisiertAm",
                table: "Fraktionen");
        }
    }
}
