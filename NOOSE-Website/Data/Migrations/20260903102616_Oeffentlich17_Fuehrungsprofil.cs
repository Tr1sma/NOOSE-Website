using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich17_Fuehrungsprofil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OeffentlicheFuehrungsprofile",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OeffentlicherSchluessel = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnzeigeName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Dienstgradbezeichnung = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Funktion = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    FotoDateiname = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FotoTyp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VeroeffentlichtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VeroeffentlichtVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_OeffentlicheFuehrungsprofile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OeffentlicheFuehrungsprofile_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFuehrungsprofile_AgentId",
                table: "OeffentlicheFuehrungsprofile",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFuehrungsprofile_OeffentlicherSchluessel",
                table: "OeffentlicheFuehrungsprofile",
                column: "OeffentlicherSchluessel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OeffentlicheFuehrungsprofile_VeroeffentlichtAm_Sortierung",
                table: "OeffentlicheFuehrungsprofile",
                columns: new[] { "VeroeffentlichtAm", "Sortierung" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OeffentlicheFuehrungsprofile");
        }
    }
}
