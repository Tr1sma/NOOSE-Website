using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich09_Belohnung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HinweisBelohnungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BelegNummer = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HinweisId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnteilId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Betrag = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KassenBuchungId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SelbstAusgezahltAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AusgezahltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HinweisBelohnungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HinweisBelohnungen_FahndungKopfgeldAnteile_AnteilId",
                        column: x => x.AnteilId,
                        principalTable: "FahndungKopfgeldAnteile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HinweisBelohnungen_Hinweise_HinweisId",
                        column: x => x.HinweisId,
                        principalTable: "Hinweise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HinweisBelohnungen_AnteilId",
                table: "HinweisBelohnungen",
                column: "AnteilId");

            migrationBuilder.CreateIndex(
                name: "IX_HinweisBelohnungen_BelegNummer",
                table: "HinweisBelohnungen",
                column: "BelegNummer");

            migrationBuilder.CreateIndex(
                name: "IX_HinweisBelohnungen_HinweisId",
                table: "HinweisBelohnungen",
                column: "HinweisId");

            migrationBuilder.CreateIndex(
                name: "IX_HinweisBelohnungen_KassenBuchungId",
                table: "HinweisBelohnungen",
                column: "KassenBuchungId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HinweisBelohnungen");
        }
    }
}
