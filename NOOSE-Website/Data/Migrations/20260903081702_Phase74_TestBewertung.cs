using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase74_TestBewertung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BewertetAm",
                table: "BewerbungTestZuweisungen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BewertetVon",
                table: "BewerbungTestZuweisungen",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ErgebnisMaxPunkte",
                table: "BewerbungTestZuweisungen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ErgebnisPunkte",
                table: "BewerbungTestZuweisungen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManuellPunkte",
                table: "BewerbungTestAntworten",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BewertetAm",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "BewertetVon",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "ErgebnisMaxPunkte",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "ErgebnisPunkte",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "ManuellPunkte",
                table: "BewerbungTestAntworten");
        }
    }
}
