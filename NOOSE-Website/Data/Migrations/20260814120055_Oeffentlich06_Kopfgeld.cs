using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich06_Kopfgeld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KopfgeldAnteilId",
                table: "Antraege",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FahndungKopfgeldAnteile",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FahndungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Herkunft = table.Column<int>(type: "int", nullable: false),
                    Betrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Konto = table.Column<int>(type: "int", nullable: true),
                    StifterAgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KassenBuchungId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Zeitpunkt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ZurueckgezogenGrund = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
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
                    table.PrimaryKey("PK_FahndungKopfgeldAnteile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FahndungKopfgeldAnteile_AspNetUsers_StifterAgentId",
                        column: x => x.StifterAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FahndungKopfgeldAnteile_OeffentlicheFahndungen_FahndungId",
                        column: x => x.FahndungId,
                        principalTable: "OeffentlicheFahndungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FahndungKopfgeldAnteile_FahndungId_Status",
                table: "FahndungKopfgeldAnteile",
                columns: new[] { "FahndungId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FahndungKopfgeldAnteile_KassenBuchungId",
                table: "FahndungKopfgeldAnteile",
                column: "KassenBuchungId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FahndungKopfgeldAnteile_StifterAgentId",
                table: "FahndungKopfgeldAnteile",
                column: "StifterAgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FahndungKopfgeldAnteile");

            migrationBuilder.DropColumn(
                name: "KopfgeldAnteilId",
                table: "Antraege");
        }
    }
}
