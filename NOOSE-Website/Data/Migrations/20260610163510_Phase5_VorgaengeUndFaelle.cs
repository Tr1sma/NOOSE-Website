using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_VorgaengeUndFaelle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vorgaenge",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Typ = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Beschreibung = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zusammenfassung = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Abschlussvermerk = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AbgeschlossenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Einstufung = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Vorgaenge", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VorgangAgenten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VorgangId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IstFallfuehrer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VorgangAgenten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VorgangAgenten_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VorgangAgenten_Vorgaenge_VorgangId",
                        column: x => x.VorgangId,
                        principalTable: "Vorgaenge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Vorgaenge_Aktenzeichen",
                table: "Vorgaenge",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vorgaenge_IstVerschlusssache",
                table: "Vorgaenge",
                column: "IstVerschlusssache");

            migrationBuilder.CreateIndex(
                name: "IX_Vorgaenge_Status",
                table: "Vorgaenge",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vorgaenge_Titel",
                table: "Vorgaenge",
                column: "Titel");

            migrationBuilder.CreateIndex(
                name: "IX_VorgangAgenten_AgentId",
                table: "VorgangAgenten",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_VorgangAgenten_VorgangId_AgentId",
                table: "VorgangAgenten",
                columns: new[] { "VorgangId", "AgentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VorgangAgenten");

            migrationBuilder.DropTable(
                name: "Vorgaenge");
        }
    }
}
