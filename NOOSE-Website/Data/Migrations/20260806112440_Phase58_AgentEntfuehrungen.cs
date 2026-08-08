using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase58_AgentEntfuehrungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentEntfuehrungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpferAgentId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaeterTyp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaeterId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zeitpunkt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FreigelassenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Ort = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Wahrheitsserum = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Informationsabfluss = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LeakKategorien = table.Column<int>(type: "int", nullable: false),
                    Schweregrad = table.Column<int>(type: "int", nullable: false),
                    Notizen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ausgang = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AgentEntfuehrungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentEntfuehrungen_AspNetUsers_OpferAgentId",
                        column: x => x.OpferAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EntfuehrungKompromittierungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntfuehrungId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZielTyp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZielId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notiz = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NormalVonId = table.Column<string>(type: "longtext", nullable: true)
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
                    table.PrimaryKey("PK_EntfuehrungKompromittierungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntfuehrungKompromittierungen_AgentEntfuehrungen_Entfuehrung~",
                        column: x => x.EntfuehrungId,
                        principalTable: "AgentEntfuehrungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEntfuehrungen_Aktenzeichen",
                table: "AgentEntfuehrungen",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentEntfuehrungen_OpferAgentId",
                table: "AgentEntfuehrungen",
                column: "OpferAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEntfuehrungen_TaeterTyp_TaeterId",
                table: "AgentEntfuehrungen",
                columns: new[] { "TaeterTyp", "TaeterId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentEntfuehrungen_Zeitpunkt",
                table: "AgentEntfuehrungen",
                column: "Zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "IX_EntfuehrungKompromittierungen_EntfuehrungId_ZielTyp_ZielId",
                table: "EntfuehrungKompromittierungen",
                columns: new[] { "EntfuehrungId", "ZielTyp", "ZielId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntfuehrungKompromittierungen_ZielTyp_ZielId_Status",
                table: "EntfuehrungKompromittierungen",
                columns: new[] { "ZielTyp", "ZielId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntfuehrungKompromittierungen");

            migrationBuilder.DropTable(
                name: "AgentEntfuehrungen");
        }
    }
}
