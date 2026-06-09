using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4a_FraktionenUndVerallgemeinerung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- EinstufungVerlauf: polymorph machen (EntitaetTyp/EntitaetId), Bestandsdaten erst sichern ---
            migrationBuilder.AddColumn<string>(
                name: "EntitaetId",
                table: "EinstufungVerlauf",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EntitaetTyp",
                table: "EinstufungVerlauf",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Backfill: bestehende Verlauf-Einträge gehörten ausnahmslos zu Personen.
            migrationBuilder.Sql("UPDATE `EinstufungVerlauf` SET `EntitaetTyp` = 'Person', `EntitaetId` = `PersonId`;");

            migrationBuilder.DropForeignKey(
                name: "FK_EinstufungVerlauf_Personen_PersonId",
                table: "EinstufungVerlauf");

            migrationBuilder.DropIndex(
                name: "IX_EinstufungVerlauf_PersonId",
                table: "EinstufungVerlauf");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "EinstufungVerlauf");

            migrationBuilder.CreateIndex(
                name: "IX_EinstufungVerlauf_EntitaetTyp_EntitaetId",
                table: "EinstufungVerlauf",
                columns: new[] { "EntitaetTyp", "EntitaetId" });

            // --- AktenzeichenZaehler: zusammengesetzter Schlüssel (Praefix, Jahr) inkl. Backfill ---
            migrationBuilder.DropPrimaryKey(
                name: "PK_AktenzeichenZaehler",
                table: "AktenzeichenZaehler");

            migrationBuilder.AddColumn<string>(
                name: "Praefix",
                table: "AktenzeichenZaehler",
                type: "varchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Backfill: bestehende Zähler gehörten zu Personen (Präfix „P").
            migrationBuilder.Sql("UPDATE `AktenzeichenZaehler` SET `Praefix` = 'P';");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AktenzeichenZaehler",
                table: "AktenzeichenZaehler",
                columns: new[] { "Praefix", "Jahr" });

            migrationBuilder.CreateTable(
                name: "Fraktionen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Funk = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Darkchat = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ausstellungszeiten = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Erkennungsfarbe = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ziele = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Einstufung = table.Column<int>(type: "int", nullable: false),
                    BedrohungsScore = table.Column<int>(type: "int", nullable: true),
                    IstVerschlusssache = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_Fraktionen", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FraktionLagerbestaende",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bezeichnung = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Menge = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraktionLagerbestaende", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FraktionLagerbestaende_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FraktionMitglieder",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PersonId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rang = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IstLeitung = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraktionMitglieder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FraktionMitglieder_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FraktionMitglieder_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FraktionRaenge",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bezeichnung = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraktionRaenge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FraktionRaenge_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FraktionWaffenbestaende",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bezeichnung = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Menge = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraktionWaffenbestaende", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FraktionWaffenbestaende_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Fraktionen_Aktenzeichen",
                table: "Fraktionen",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fraktionen_IstVerschlusssache",
                table: "Fraktionen",
                column: "IstVerschlusssache");

            migrationBuilder.CreateIndex(
                name: "IX_Fraktionen_Name",
                table: "Fraktionen",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FraktionLagerbestaende_FraktionId",
                table: "FraktionLagerbestaende",
                column: "FraktionId");

            migrationBuilder.CreateIndex(
                name: "IX_FraktionMitglieder_FraktionId_PersonId",
                table: "FraktionMitglieder",
                columns: new[] { "FraktionId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FraktionMitglieder_PersonId",
                table: "FraktionMitglieder",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_FraktionRaenge_FraktionId",
                table: "FraktionRaenge",
                column: "FraktionId");

            migrationBuilder.CreateIndex(
                name: "IX_FraktionWaffenbestaende_FraktionId",
                table: "FraktionWaffenbestaende",
                column: "FraktionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FraktionLagerbestaende");

            migrationBuilder.DropTable(
                name: "FraktionMitglieder");

            migrationBuilder.DropTable(
                name: "FraktionRaenge");

            migrationBuilder.DropTable(
                name: "FraktionWaffenbestaende");

            migrationBuilder.DropTable(
                name: "Fraktionen");

            // --- AktenzeichenZaehler zurück auf Einzelschlüssel (Jahr) ---
            migrationBuilder.DropPrimaryKey(
                name: "PK_AktenzeichenZaehler",
                table: "AktenzeichenZaehler");

            // Nicht-Personen-Zähler entfernen, damit Jahr wieder eindeutig ist.
            migrationBuilder.Sql("DELETE FROM `AktenzeichenZaehler` WHERE `Praefix` <> 'P';");

            migrationBuilder.DropColumn(
                name: "Praefix",
                table: "AktenzeichenZaehler");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AktenzeichenZaehler",
                table: "AktenzeichenZaehler",
                column: "Jahr");

            // --- EinstufungVerlauf zurück auf PersonId ---
            migrationBuilder.DropIndex(
                name: "IX_EinstufungVerlauf_EntitaetTyp_EntitaetId",
                table: "EinstufungVerlauf");

            migrationBuilder.AddColumn<string>(
                name: "PersonId",
                table: "EinstufungVerlauf",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Nur Personen-Verläufe lassen sich zurückführen; übrige (Fraktion etc.) entfernen.
            migrationBuilder.Sql("DELETE FROM `EinstufungVerlauf` WHERE `EntitaetTyp` <> 'Person';");
            migrationBuilder.Sql("UPDATE `EinstufungVerlauf` SET `PersonId` = `EntitaetId`;");

            migrationBuilder.DropColumn(
                name: "EntitaetId",
                table: "EinstufungVerlauf");

            migrationBuilder.DropColumn(
                name: "EntitaetTyp",
                table: "EinstufungVerlauf");

            migrationBuilder.CreateIndex(
                name: "IX_EinstufungVerlauf_PersonId",
                table: "EinstufungVerlauf",
                column: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_EinstufungVerlauf_Personen_PersonId",
                table: "EinstufungVerlauf",
                column: "PersonId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
