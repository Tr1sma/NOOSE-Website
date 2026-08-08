using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase48_ErmittlungshinweisIgnorierung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GeaendertAm",
                table: "PersonFotos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeaendertVonId",
                table: "PersonFotos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeloeschtAm",
                table: "PersonFotos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeloeschtVonId",
                table: "PersonFotos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstGeloescht",
                table: "PersonFotos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeaendertAm",
                table: "FraktionFotos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeaendertVonId",
                table: "FraktionFotos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeloeschtAm",
                table: "FraktionFotos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeloeschtVonId",
                table: "FraktionFotos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstGeloescht",
                table: "FraktionFotos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HinweisIgnorierungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HinweisSchluessel = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<int>(type: "int", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IstGeloescht = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GeloeschtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeloeschtVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HinweisIgnorierungen", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HinweisIgnorierungen_HinweisSchluessel",
                table: "HinweisIgnorierungen",
                column: "HinweisSchluessel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HinweisIgnorierungen");

            migrationBuilder.DropColumn(
                name: "GeaendertAm",
                table: "PersonFotos");

            migrationBuilder.DropColumn(
                name: "GeaendertVonId",
                table: "PersonFotos");

            migrationBuilder.DropColumn(
                name: "GeloeschtAm",
                table: "PersonFotos");

            migrationBuilder.DropColumn(
                name: "GeloeschtVonId",
                table: "PersonFotos");

            migrationBuilder.DropColumn(
                name: "IstGeloescht",
                table: "PersonFotos");

            migrationBuilder.DropColumn(
                name: "GeaendertAm",
                table: "FraktionFotos");

            migrationBuilder.DropColumn(
                name: "GeaendertVonId",
                table: "FraktionFotos");

            migrationBuilder.DropColumn(
                name: "GeloeschtAm",
                table: "FraktionFotos");

            migrationBuilder.DropColumn(
                name: "GeloeschtVonId",
                table: "FraktionFotos");

            migrationBuilder.DropColumn(
                name: "IstGeloescht",
                table: "FraktionFotos");
        }
    }
}
