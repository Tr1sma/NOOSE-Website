using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich14_Warnungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IstOeffentlich",
                table: "Gesetze",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OeffentlicheWarnungen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntwurfHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InhaltTitel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InhaltHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GueltigBis = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VeroeffentlichtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VeroeffentlichtVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_OeffentlicheWarnungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OeffentlicheWarnungen_AspNetUsers_VeroeffentlichtVonId",
                        column: x => x.VeroeffentlichtVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Gesetze_IstOeffentlich",
                table: "Gesetze",
                column: "IstOeffentlich");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheWarnungen_Status_GueltigBis",
                table: "OeffentlicheWarnungen",
                columns: new[] { "Status", "GueltigBis" });

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheWarnungen_VeroeffentlichtVonId",
                table: "OeffentlicheWarnungen",
                column: "VeroeffentlichtVonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OeffentlicheWarnungen");

            migrationBuilder.DropIndex(
                name: "IX_Gesetze_IstOeffentlich",
                table: "Gesetze");

            migrationBuilder.DropColumn(
                name: "IstOeffentlich",
                table: "Gesetze");
        }
    }
}
