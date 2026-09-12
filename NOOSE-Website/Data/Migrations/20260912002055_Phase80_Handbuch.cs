using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase80_Handbuch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HandbuchKapitel",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Sichtbar = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SeedSchluessel = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeedRevision = table.Column<int>(type: "int", nullable: false),
                    IstAngepasst = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_HandbuchKapitel", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HandbuchArtikel",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KapitelId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kurzbeschreibung = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InhaltHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RollenspielHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DiagrammSchluessel = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NavSchluessel = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    Sichtbar = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SeedSchluessel = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeedRevision = table.Column<int>(type: "int", nullable: false),
                    IstAngepasst = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_HandbuchArtikel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandbuchArtikel_HandbuchKapitel_KapitelId",
                        column: x => x.KapitelId,
                        principalTable: "HandbuchKapitel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HandbuchBegriffe",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Begriff = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Synonyme = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kurzdefinition = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErklaerungHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtikelId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Sichtbar = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SeedSchluessel = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeedRevision = table.Column<int>(type: "int", nullable: false),
                    IstAngepasst = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandbuchBegriffe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandbuchBegriffe_HandbuchArtikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "HandbuchArtikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HandbuchSchritte",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArtikelId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nummer = table.Column<int>(type: "int", nullable: false),
                    IconName = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Titel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false)
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
                    table.PrimaryKey("PK_HandbuchSchritte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandbuchSchritte_HandbuchArtikel_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "HandbuchArtikel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchArtikel_KapitelId_Sichtbar_Reihenfolge",
                table: "HandbuchArtikel",
                columns: new[] { "KapitelId", "Sichtbar", "Reihenfolge" });

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchArtikel_NavSchluessel",
                table: "HandbuchArtikel",
                column: "NavSchluessel");

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchArtikel_SeedSchluessel",
                table: "HandbuchArtikel",
                column: "SeedSchluessel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchArtikel_Slug",
                table: "HandbuchArtikel",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchBegriffe_ArtikelId",
                table: "HandbuchBegriffe",
                column: "ArtikelId");

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchBegriffe_Begriff",
                table: "HandbuchBegriffe",
                column: "Begriff",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchBegriffe_SeedSchluessel",
                table: "HandbuchBegriffe",
                column: "SeedSchluessel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchBegriffe_Sichtbar",
                table: "HandbuchBegriffe",
                column: "Sichtbar");

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchKapitel_SeedSchluessel",
                table: "HandbuchKapitel",
                column: "SeedSchluessel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchKapitel_Sichtbar_Reihenfolge",
                table: "HandbuchKapitel",
                columns: new[] { "Sichtbar", "Reihenfolge" });

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchKapitel_Slug",
                table: "HandbuchKapitel",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandbuchSchritte_ArtikelId_Nummer",
                table: "HandbuchSchritte",
                columns: new[] { "ArtikelId", "Nummer" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandbuchBegriffe");

            migrationBuilder.DropTable(
                name: "HandbuchSchritte");

            migrationBuilder.DropTable(
                name: "HandbuchArtikel");

            migrationBuilder.DropTable(
                name: "HandbuchKapitel");
        }
    }
}
