using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase61_Finanzierungsantraege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinanzierungJson",
                table: "Lageberichte",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "Finanzierungsbudget",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finanzierungsantraege",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Begruendung = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeantragteSumme = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeantragterZuschuss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GenehmigterZuschuss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetJahr = table.Column<int>(type: "int", nullable: true),
                    BudgetMonat = table.Column<int>(type: "int", nullable: true),
                    EntscheiderName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntschiedenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Entscheidungsnotiz = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ueberschreitungsbetrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UeberschreitungsBegruendung = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AusgezahltAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AusgezahltVonName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KassenBuchungId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
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
                    table.PrimaryKey("PK_Finanzierungsantraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finanzierungsantraege_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Finanzierungsbudgetperioden",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Jahr = table.Column<int>(type: "int", nullable: false),
                    Monat = table.Column<int>(type: "int", nullable: false),
                    Grundbudget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UebertragEin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Verbraucht = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UebertragAus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Uebertragsprozent = table.Column<int>(type: "int", nullable: false),
                    DienstgradBeiAbschluss = table.Column<int>(type: "int", nullable: true),
                    AbgeschlossenAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finanzierungsbudgetperioden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finanzierungsbudgetperioden_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Finanzierungspositionen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kategorie = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Einzelpreis = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Zuschussanteil = table.Column<int>(type: "int", nullable: false),
                    MindestDienstgrad = table.Column<int>(type: "int", nullable: false),
                    MaxMenge = table.Column<int>(type: "int", nullable: false),
                    IstAktiv = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Finanzierungspositionen", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Finanzierungsantragspositionen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AntragId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PositionId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bezeichnung = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kategorie = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Einzelpreis = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Zuschussanteil = table.Column<int>(type: "int", nullable: false),
                    Menge = table.Column<int>(type: "int", nullable: false),
                    GenehmigteMenge = table.Column<int>(type: "int", nullable: true),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finanzierungsantragspositionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finanzierungsantragspositionen_Finanzierungsantraege_AntragId",
                        column: x => x.AntragId,
                        principalTable: "Finanzierungsantraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Finanzierungsantragspositionen_Finanzierungspositionen_Posit~",
                        column: x => x.PositionId,
                        principalTable: "Finanzierungspositionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantraege_AgentId_Status",
                table: "Finanzierungsantraege",
                columns: new[] { "AgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantraege_Aktenzeichen",
                table: "Finanzierungsantraege",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantraege_BudgetJahr_BudgetMonat",
                table: "Finanzierungsantraege",
                columns: new[] { "BudgetJahr", "BudgetMonat" });

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantraege_KassenBuchungId",
                table: "Finanzierungsantraege",
                column: "KassenBuchungId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantraege_Status",
                table: "Finanzierungsantraege",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantragspositionen_AntragId",
                table: "Finanzierungsantragspositionen",
                column: "AntragId");

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsantragspositionen_PositionId",
                table: "Finanzierungsantragspositionen",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungsbudgetperioden_AgentId_Jahr_Monat",
                table: "Finanzierungsbudgetperioden",
                columns: new[] { "AgentId", "Jahr", "Monat" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungspositionen_IstAktiv",
                table: "Finanzierungspositionen",
                column: "IstAktiv");

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungspositionen_Kategorie",
                table: "Finanzierungspositionen",
                column: "Kategorie");

            migrationBuilder.CreateIndex(
                name: "IX_Finanzierungspositionen_Name",
                table: "Finanzierungspositionen",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finanzierungsantragspositionen");

            migrationBuilder.DropTable(
                name: "Finanzierungsbudgetperioden");

            migrationBuilder.DropTable(
                name: "Finanzierungsantraege");

            migrationBuilder.DropTable(
                name: "Finanzierungspositionen");

            migrationBuilder.DropColumn(
                name: "FinanzierungJson",
                table: "Lageberichte");

            migrationBuilder.DropColumn(
                name: "Finanzierungsbudget",
                table: "AspNetUsers");
        }
    }
}
