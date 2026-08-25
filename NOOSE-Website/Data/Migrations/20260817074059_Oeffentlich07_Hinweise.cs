using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich07_Hinweise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hinweise",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuergerProfilId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnonymGewuenscht = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FahndungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnhangDateiname = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnhangOriginalname = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnhangTyp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BearbeiterId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DublettenGruppeId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Prioritaet = table.Column<int>(type: "int", nullable: false),
                    AnonymitaetAufgeloestAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AnonymitaetAufgeloestVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZuletztGelesenBuergerAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_Hinweise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hinweise_AspNetUsers_BearbeiterId",
                        column: x => x.BearbeiterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hinweise_BuergerProfile_BuergerProfilId",
                        column: x => x.BuergerProfilId,
                        principalTable: "BuergerProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hinweise_OeffentlicheFahndungen_FahndungId",
                        column: x => x.FahndungId,
                        principalTable: "OeffentlicheFahndungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HinweisNachrichten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HinweisId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zielgruppe = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VonBuerger = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AutorAgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
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
                    table.PrimaryKey("PK_HinweisNachrichten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HinweisNachrichten_AspNetUsers_AutorAgentId",
                        column: x => x.AutorAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HinweisNachrichten_Hinweise_HinweisId",
                        column: x => x.HinweisId,
                        principalTable: "Hinweise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_Aktenzeichen",
                table: "Hinweise",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_BearbeiterId",
                table: "Hinweise",
                column: "BearbeiterId");

            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_BuergerProfilId_ErstelltAm",
                table: "Hinweise",
                columns: new[] { "BuergerProfilId", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_FahndungId",
                table: "Hinweise",
                column: "FahndungId");

            migrationBuilder.CreateIndex(
                name: "IX_Hinweise_Status_ErstelltAm",
                table: "Hinweise",
                columns: new[] { "Status", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_HinweisNachrichten_AutorAgentId",
                table: "HinweisNachrichten",
                column: "AutorAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_HinweisNachrichten_HinweisId_ErstelltAm",
                table: "HinweisNachrichten",
                columns: new[] { "HinweisId", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_HinweisNachrichten_HinweisId_Zielgruppe",
                table: "HinweisNachrichten",
                columns: new[] { "HinweisId", "Zielgruppe" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HinweisNachrichten");

            migrationBuilder.DropTable(
                name: "Hinweise");
        }
    }
}
