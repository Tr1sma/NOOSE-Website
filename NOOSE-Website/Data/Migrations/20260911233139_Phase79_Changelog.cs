using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase79_Changelog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aenderungsfassungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Datum = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuildNummer = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Sichtbar = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aenderungsfassungen", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Aenderungseintraege",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FassungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Titel = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bereich = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Sichtbar = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SeedSchluessel = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeedRevision = table.Column<int>(type: "int", nullable: false),
                    IstAngepasst = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_Aenderungseintraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aenderungseintraege_Aenderungsfassungen_FassungId",
                        column: x => x.FassungId,
                        principalTable: "Aenderungsfassungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungseintraege_FassungId_Sichtbar_Reihenfolge",
                table: "Aenderungseintraege",
                columns: new[] { "FassungId", "Sichtbar", "Reihenfolge" });

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungseintraege_SeedSchluessel",
                table: "Aenderungseintraege",
                column: "SeedSchluessel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungsfassungen_Sichtbar_Datum_Reihenfolge",
                table: "Aenderungsfassungen",
                columns: new[] { "Sichtbar", "Datum", "Reihenfolge" });

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungsfassungen_Version",
                table: "Aenderungsfassungen",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aenderungseintraege");

            migrationBuilder.DropTable(
                name: "Aenderungsfassungen");
        }
    }
}
