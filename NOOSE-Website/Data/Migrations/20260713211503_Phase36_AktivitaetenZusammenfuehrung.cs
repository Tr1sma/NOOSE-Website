using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase36_AktivitaetenZusammenfuehrung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Carry faction activities into the unified AgentActivity model, keeping the PK so links map 1:1.
            // Legacy descriptions are plain text: HTML-escape and wrap so they render safely as rich text.
            migrationBuilder.Sql(@"
INSERT INTO Aktivitaeten
    (Id, Titel, Art, Datum, InhaltHtml, ErstelltAm, ErstelltVonId, GeaendertAm, GeaendertVonId, IstGeloescht, GeloeschtAm, GeloeschtVonId)
SELECT
    fa.Id,
    fa.Titel,
    fa.Art,
    fa.Zeitpunkt,
    CONCAT(
        CASE WHEN fa.Ort IS NOT NULL AND fa.Ort <> ''
             THEN CONCAT('<p><em>Ort: ', REPLACE(REPLACE(REPLACE(fa.Ort, '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), '</em></p>')
             ELSE '' END,
        CASE WHEN fa.Beschreibung IS NOT NULL AND fa.Beschreibung <> ''
             THEN CONCAT('<p>', REPLACE(REPLACE(REPLACE(REPLACE(fa.Beschreibung, '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), CHAR(10), '<br>'), '</p>')
             ELSE '' END
    ),
    fa.ErstelltAm, fa.ErstelltVonId, fa.GeaendertAm, fa.GeaendertVonId, fa.IstGeloescht, fa.GeloeschtAm, fa.GeloeschtVonId
FROM FraktionAktivitaeten fa;");

            migrationBuilder.Sql(@"
INSERT INTO AktivitaetVerknuepfungen (Id, AgentActivityId, EntitaetTyp, EntitaetId)
SELECT UUID(), fa.Id, 'Faction', fa.FraktionId
FROM FraktionAktivitaeten fa
WHERE fa.FraktionId IS NOT NULL AND fa.FraktionId <> '';");

            migrationBuilder.DropTable(
                name: "FraktionAktivitaeten");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FraktionAktivitaeten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeloeschtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeloeschtVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IstGeloescht = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Art = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ort = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zeitpunkt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraktionAktivitaeten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FraktionAktivitaeten_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FraktionAktivitaeten_Art",
                table: "FraktionAktivitaeten",
                column: "Art");

            migrationBuilder.CreateIndex(
                name: "IX_FraktionAktivitaeten_FraktionId",
                table: "FraktionAktivitaeten",
                column: "FraktionId");
        }
    }
}
