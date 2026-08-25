using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich13_Einspruch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FahndungEinsprueche",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FahndungId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuergerProfilId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Entscheidungsnotiz = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntschiedenVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntschiedenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VorgangId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_FahndungEinsprueche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FahndungEinsprueche_AspNetUsers_EntschiedenVonId",
                        column: x => x.EntschiedenVonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FahndungEinsprueche_BuergerProfile_BuergerProfilId",
                        column: x => x.BuergerProfilId,
                        principalTable: "BuergerProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FahndungEinsprueche_OeffentlicheFahndungen_FahndungId",
                        column: x => x.FahndungId,
                        principalTable: "OeffentlicheFahndungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FahndungEinsprueche_Vorgaenge_VorgangId",
                        column: x => x.VorgangId,
                        principalTable: "Vorgaenge",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FahndungEinsprueche_Aktenzeichen",
                table: "FahndungEinsprueche",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FahndungEinsprueche_BuergerProfilId_ErstelltAm",
                table: "FahndungEinsprueche",
                columns: new[] { "BuergerProfilId", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_FahndungEinsprueche_EntschiedenVonId",
                table: "FahndungEinsprueche",
                column: "EntschiedenVonId");

            migrationBuilder.CreateIndex(
                name: "IX_FahndungEinsprueche_FahndungId_Status",
                table: "FahndungEinsprueche",
                columns: new[] { "FahndungId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FahndungEinsprueche_Status_ErstelltAm",
                table: "FahndungEinsprueche",
                columns: new[] { "Status", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_FahndungEinsprueche_VorgangId",
                table: "FahndungEinsprueche",
                column: "VorgangId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FahndungEinsprueche");
        }
    }
}
