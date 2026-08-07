using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase65_KiKontingente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "KiKontingent",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KiAnfragen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zeitpunkt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BudgetJahr = table.Column<int>(type: "int", nullable: false),
                    BudgetWoche = table.Column<int>(type: "int", nullable: false),
                    Funktion = table.Column<int>(type: "int", nullable: false),
                    Modell = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Anbieter = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokensEingabe = table.Column<int>(type: "int", nullable: false),
                    TokensAusgabe = table.Column<int>(type: "int", nullable: false),
                    TokensCache = table.Column<int>(type: "int", nullable: false),
                    TokensDenken = table.Column<int>(type: "int", nullable: false),
                    KostenUsd = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    KontingentTokens = table.Column<long>(type: "bigint", nullable: false),
                    DauerMs = table.Column<int>(type: "int", nullable: false),
                    Werkzeugrunden = table.Column<int>(type: "int", nullable: false),
                    Erfolg = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Fehler = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Eingabe = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Antwort = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kontextrefs = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EingabeFingerabdruck = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Auffaellig = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Auffaelligkeit = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KiAnfragen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KiAnfragen_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "KiKontingentkorrekturen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Jahr = table.Column<int>(type: "int", nullable: false),
                    Woche = table.Column<int>(type: "int", nullable: false),
                    Tokens = table.Column<long>(type: "bigint", nullable: false),
                    Grund = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltVonName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KiKontingentkorrekturen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KiKontingentkorrekturen_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "KiKontingentperioden",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Jahr = table.Column<int>(type: "int", nullable: false),
                    Woche = table.Column<int>(type: "int", nullable: false),
                    Grundkontingent = table.Column<long>(type: "bigint", nullable: false),
                    UebertragEin = table.Column<long>(type: "bigint", nullable: false),
                    Verbraucht = table.Column<long>(type: "bigint", nullable: false),
                    UebertragAus = table.Column<long>(type: "bigint", nullable: false),
                    Uebertragsprozent = table.Column<int>(type: "int", nullable: false),
                    DienstgradBeiAbschluss = table.Column<int>(type: "int", nullable: true),
                    AbgeschlossenAm = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KiKontingentperioden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KiKontingentperioden_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_KiAnfragen_AgentId_BudgetJahr_BudgetWoche",
                table: "KiAnfragen",
                columns: new[] { "AgentId", "BudgetJahr", "BudgetWoche" });

            migrationBuilder.CreateIndex(
                name: "IX_KiAnfragen_AgentId_EingabeFingerabdruck",
                table: "KiAnfragen",
                columns: new[] { "AgentId", "EingabeFingerabdruck" });

            migrationBuilder.CreateIndex(
                name: "IX_KiAnfragen_AgentId_Zeitpunkt",
                table: "KiAnfragen",
                columns: new[] { "AgentId", "Zeitpunkt" });

            migrationBuilder.CreateIndex(
                name: "IX_KiAnfragen_Auffaellig_Zeitpunkt",
                table: "KiAnfragen",
                columns: new[] { "Auffaellig", "Zeitpunkt" });

            migrationBuilder.CreateIndex(
                name: "IX_KiAnfragen_Funktion_Zeitpunkt",
                table: "KiAnfragen",
                columns: new[] { "Funktion", "Zeitpunkt" });

            migrationBuilder.CreateIndex(
                name: "IX_KiAnfragen_Zeitpunkt",
                table: "KiAnfragen",
                column: "Zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "IX_KiKontingentkorrekturen_AgentId_Jahr_Woche",
                table: "KiKontingentkorrekturen",
                columns: new[] { "AgentId", "Jahr", "Woche" });

            migrationBuilder.CreateIndex(
                name: "IX_KiKontingentperioden_AgentId_Jahr_Woche",
                table: "KiKontingentperioden",
                columns: new[] { "AgentId", "Jahr", "Woche" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KiAnfragen");

            migrationBuilder.DropTable(
                name: "KiKontingentkorrekturen");

            migrationBuilder.DropTable(
                name: "KiKontingentperioden");

            migrationBuilder.DropColumn(
                name: "KiKontingent",
                table: "AspNetUsers");
        }
    }
}
