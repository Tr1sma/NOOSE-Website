using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich04_Fahndung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VeroeffentlichungFahndungId",
                table: "Antraege",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OeffentlicheFahndungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnzeigeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AliaseText = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FotoDateiname = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FotoTyp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FotoQuellId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VorwurfHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LetzteGegend = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FahrzeugText = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OeffentlicheGefahrenstufe = table.Column<int>(type: "int", nullable: false),
                    AblaufDatum = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KopfgeldIstObergrenze = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    VeroeffentlichtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VeroeffentlichtVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZurueckgezogenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ZurueckgezogenGrund = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GefasstAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AufrufZaehler = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_OeffentlicheFahndungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OeffentlicheFahndungen_AspNetUsers_VeroeffentlichtVonId",
                        column: x => x.VeroeffentlichtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OeffentlicheFahndungen_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OeffentlicheFahndungen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFahndungen_AblaufDatum",
                table: "OeffentlicheFahndungen",
                column: "AblaufDatum");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFahndungen_Aktenzeichen",
                table: "OeffentlicheFahndungen",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFahndungen_FraktionId",
                table: "OeffentlicheFahndungen",
                column: "FraktionId");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFahndungen_PersonId",
                table: "OeffentlicheFahndungen",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFahndungen_Status_VeroeffentlichtAm",
                table: "OeffentlicheFahndungen",
                columns: new[] { "Status", "VeroeffentlichtAm" });

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFahndungen_VeroeffentlichtVonId",
                table: "OeffentlicheFahndungen",
                column: "VeroeffentlichtVonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OeffentlicheFahndungen");

            migrationBuilder.DropColumn(
                name: "VeroeffentlichungFahndungId",
                table: "Antraege");
        }
    }
}
