using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich05_Warnhinweise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Warnhinweise",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bezeichnung = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Farbe = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    IstAktiv = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnhinweise", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FahndungWarnhinweise",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FahndungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WarnhinweisId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FahndungWarnhinweise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FahndungWarnhinweise_OeffentlicheFahndungen_FahndungId",
                        column: x => x.FahndungId,
                        principalTable: "OeffentlicheFahndungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FahndungWarnhinweise_Warnhinweise_WarnhinweisId",
                        column: x => x.WarnhinweisId,
                        principalTable: "Warnhinweise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FahndungWarnhinweise_FahndungId_WarnhinweisId",
                table: "FahndungWarnhinweise",
                columns: new[] { "FahndungId", "WarnhinweisId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FahndungWarnhinweise_WarnhinweisId",
                table: "FahndungWarnhinweise",
                column: "WarnhinweisId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnhinweise_Bezeichnung",
                table: "Warnhinweise",
                column: "Bezeichnung",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warnhinweise_IstAktiv_Reihenfolge",
                table: "Warnhinweise",
                columns: new[] { "IstAktiv", "Reihenfolge" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FahndungWarnhinweise");

            migrationBuilder.DropTable(
                name: "Warnhinweise");
        }
    }
}
