using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase40_BesprechungenUndAbmeldungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Abmeldungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VonDatum = table.Column<DateOnly>(type: "date", nullable: false),
                    BisDatum = table.Column<DateOnly>(type: "date", nullable: false),
                    Tage = table.Column<int>(type: "int", nullable: false),
                    Abmeldegrund = table.Column<int>(type: "int", nullable: false),
                    Grund = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KenntnisGenommenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    KenntnisGenommenVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KenntnisGenommenVonName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
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
                    table.PrimaryKey("PK_Abmeldungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abmeldungen_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Abmeldungen_AspNetUsers_KenntnisGenommenVonId",
                        column: x => x.KenntnisGenommenVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Besprechungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beginn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Ende = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Ort = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProtokollHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VorherigeBesprechungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErinnerungGesendetAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AnwesenheitAbgeschlossenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_Besprechungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Besprechungen_Besprechungen_VorherigeBesprechungId",
                        column: x => x.VorherigeBesprechungId,
                        principalTable: "Besprechungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BesprechungAbmeldungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BesprechungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Grund = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BesprechungAbmeldungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BesprechungAbmeldungen_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BesprechungAbmeldungen_Besprechungen_BesprechungId",
                        column: x => x.BesprechungId,
                        principalTable: "Besprechungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BesprechungAnwesenheiten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BesprechungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentCodename = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Herkunft = table.Column<int>(type: "int", nullable: false),
                    Grund = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErfasstAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErfasstVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BesprechungAnwesenheiten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BesprechungAnwesenheiten_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BesprechungAnwesenheiten_Besprechungen_BesprechungId",
                        column: x => x.BesprechungId,
                        principalTable: "Besprechungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BesprechungPunkte",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BesprechungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    NotizHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Erledigt = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErledigtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErledigtVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UebernommenVonPunktId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_BesprechungPunkte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BesprechungPunkte_Besprechungen_BesprechungId",
                        column: x => x.BesprechungId,
                        principalTable: "Besprechungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Abmeldungen_AgentId_VonDatum_BisDatum",
                table: "Abmeldungen",
                columns: new[] { "AgentId", "VonDatum", "BisDatum" });

            migrationBuilder.CreateIndex(
                name: "IX_Abmeldungen_KenntnisGenommenAm",
                table: "Abmeldungen",
                column: "KenntnisGenommenAm");

            migrationBuilder.CreateIndex(
                name: "IX_Abmeldungen_KenntnisGenommenVonId",
                table: "Abmeldungen",
                column: "KenntnisGenommenVonId");

            migrationBuilder.CreateIndex(
                name: "IX_Abmeldungen_VonDatum_BisDatum_AgentId",
                table: "Abmeldungen",
                columns: new[] { "VonDatum", "BisDatum", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_BesprechungAbmeldungen_AgentId",
                table: "BesprechungAbmeldungen",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_BesprechungAbmeldungen_BesprechungId_AgentId",
                table: "BesprechungAbmeldungen",
                columns: new[] { "BesprechungId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BesprechungAnwesenheiten_AgentId",
                table: "BesprechungAnwesenheiten",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_BesprechungAnwesenheiten_BesprechungId_AgentId",
                table: "BesprechungAnwesenheiten",
                columns: new[] { "BesprechungId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_Aktenzeichen",
                table: "Besprechungen",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_Beginn_Status_ErinnerungGesendetAm",
                table: "Besprechungen",
                columns: new[] { "Beginn", "Status", "ErinnerungGesendetAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_Status_AnwesenheitAbgeschlossenAm_Beginn",
                table: "Besprechungen",
                columns: new[] { "Status", "AnwesenheitAbgeschlossenAm", "Beginn" });

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_Titel",
                table: "Besprechungen",
                column: "Titel");

            migrationBuilder.CreateIndex(
                name: "IX_Besprechungen_VorherigeBesprechungId",
                table: "Besprechungen",
                column: "VorherigeBesprechungId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BesprechungPunkte_BesprechungId_Sortierung",
                table: "BesprechungPunkte",
                columns: new[] { "BesprechungId", "Sortierung" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Abmeldungen");

            migrationBuilder.DropTable(
                name: "BesprechungAbmeldungen");

            migrationBuilder.DropTable(
                name: "BesprechungAnwesenheiten");

            migrationBuilder.DropTable(
                name: "BesprechungPunkte");

            migrationBuilder.DropTable(
                name: "Besprechungen");
        }
    }
}
