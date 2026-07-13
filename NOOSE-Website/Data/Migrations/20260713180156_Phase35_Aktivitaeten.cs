using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase35_Aktivitaeten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aktivitaeten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Datum = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    InhaltHtml = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "varchar(255)", nullable: true)
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
                    table.PrimaryKey("PK_Aktivitaeten", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AktivitaetVorlagen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InhaltHtml = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
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
                    table.PrimaryKey("PK_AktivitaetVorlagen", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AktivitaetVerknuepfungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentActivityId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntitaetTyp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntitaetId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AktivitaetVerknuepfungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AktivitaetVerknuepfungen_Aktivitaeten_AgentActivityId",
                        column: x => x.AgentActivityId,
                        principalTable: "Aktivitaeten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_Art",
                table: "Aktivitaeten",
                column: "Art");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_Datum",
                table: "Aktivitaeten",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_ErstelltVonId",
                table: "Aktivitaeten",
                column: "ErstelltVonId");

            migrationBuilder.CreateIndex(
                name: "IX_Aktivitaeten_Titel",
                table: "Aktivitaeten",
                column: "Titel");

            migrationBuilder.CreateIndex(
                name: "IX_AktivitaetVerknuepfungen_AgentActivityId_EntitaetTyp_Entitae~",
                table: "AktivitaetVerknuepfungen",
                columns: new[] { "AgentActivityId", "EntitaetTyp", "EntitaetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AktivitaetVerknuepfungen_EntitaetTyp_EntitaetId",
                table: "AktivitaetVerknuepfungen",
                columns: new[] { "EntitaetTyp", "EntitaetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AktivitaetVorlagen_IstAktiv",
                table: "AktivitaetVorlagen",
                column: "IstAktiv");

            migrationBuilder.CreateIndex(
                name: "IX_AktivitaetVorlagen_Name",
                table: "AktivitaetVorlagen",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AktivitaetVerknuepfungen");

            migrationBuilder.DropTable(
                name: "AktivitaetVorlagen");

            migrationBuilder.DropTable(
                name: "Aktivitaeten");
        }
    }
}
