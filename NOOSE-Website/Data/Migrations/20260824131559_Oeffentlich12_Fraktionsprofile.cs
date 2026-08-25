using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich12_Fraktionsprofile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OeffentlicheFraktionsprofile",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FraktionId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnzeigeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KurzbeschreibungHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Einordnung = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OeffentlicheGefahrenstufe = table.Column<int>(type: "int", nullable: false),
                    VeroeffentlichtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VeroeffentlichtVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZurueckgezogenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ZurueckgezogenGrund = table.Column<string>(type: "longtext", nullable: true)
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
                    table.PrimaryKey("PK_OeffentlicheFraktionsprofile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OeffentlicheFraktionsprofile_AspNetUsers_VeroeffentlichtVonId",
                        column: x => x.VeroeffentlichtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OeffentlicheFraktionsprofile_Fraktionen_FraktionId",
                        column: x => x.FraktionId,
                        principalTable: "Fraktionen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFraktionsprofile_FraktionId",
                table: "OeffentlicheFraktionsprofile",
                column: "FraktionId");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFraktionsprofile_Status_VeroeffentlichtAm",
                table: "OeffentlicheFraktionsprofile",
                columns: new[] { "Status", "VeroeffentlichtAm" });

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFraktionsprofile_VeroeffentlichtVonId",
                table: "OeffentlicheFraktionsprofile",
                column: "VeroeffentlichtVonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OeffentlicheFraktionsprofile");
        }
    }
}
