using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_AktualitaetWiedervorlage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AktualitaetsSchwellen",
                columns: table => new
                {
                    AktenTyp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WarnungTage = table.Column<int>(type: "int", nullable: false),
                    VeraltetTage = table.Column<int>(type: "int", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AktualitaetsSchwellen", x => x.AktenTyp);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Wiedervorlagen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntitaetTyp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntitaetId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FaelligAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Notiz = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZustaendigerAgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Erledigt = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErledigtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErledigtVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BenachrichtigtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_Wiedervorlagen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wiedervorlagen_AspNetUsers_ZustaendigerAgentId",
                        column: x => x.ZustaendigerAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Wiedervorlagen_EntitaetTyp_EntitaetId",
                table: "Wiedervorlagen",
                columns: new[] { "EntitaetTyp", "EntitaetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Wiedervorlagen_FaelligAm_Erledigt_BenachrichtigtAm",
                table: "Wiedervorlagen",
                columns: new[] { "FaelligAm", "Erledigt", "BenachrichtigtAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Wiedervorlagen_ZustaendigerAgentId",
                table: "Wiedervorlagen",
                column: "ZustaendigerAgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AktualitaetsSchwellen");

            migrationBuilder.DropTable(
                name: "Wiedervorlagen");
        }
    }
}
