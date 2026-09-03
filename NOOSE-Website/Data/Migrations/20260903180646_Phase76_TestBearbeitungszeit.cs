using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase76_TestBearbeitungszeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BearbeitungszeitMinuten",
                table: "BewerbungTestZuweisungen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FristBis",
                table: "BewerbungTestZuweisungen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GestartetAm",
                table: "BewerbungTestZuweisungen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Versuch",
                table: "BewerbungTestZuweisungen",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "ZeitAbgelaufen",
                table: "BewerbungTestZuweisungen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ZusatzMinuten",
                table: "BewerbungTestZuweisungen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BearbeitungszeitMinuten",
                table: "BewerbungTests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BewerbungTestZuweisungen_AbgeschlossenAm_FristBis",
                table: "BewerbungTestZuweisungen",
                columns: new[] { "AbgeschlossenAm", "FristBis" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BewerbungTestZuweisungen_AbgeschlossenAm_FristBis",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "BearbeitungszeitMinuten",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "FristBis",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "GestartetAm",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "Versuch",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "ZeitAbgelaufen",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "ZusatzMinuten",
                table: "BewerbungTestZuweisungen");

            migrationBuilder.DropColumn(
                name: "BearbeitungszeitMinuten",
                table: "BewerbungTests");
        }
    }
}
