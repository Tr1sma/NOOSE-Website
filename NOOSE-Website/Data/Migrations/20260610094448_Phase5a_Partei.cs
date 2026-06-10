using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5a_Partei : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parteien",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ziele = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bemerkungen = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
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
                    table.PrimaryKey("PK_Parteien", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ParteiAgenten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParteiId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", nullable: false)
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
                    table.PrimaryKey("PK_ParteiAgenten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParteiAgenten_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParteiAgenten_Parteien_ParteiId",
                        column: x => x.ParteiId,
                        principalTable: "Parteien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ParteiMitglieder",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParteiId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PersonId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rolle = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IstLeitung = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_ParteiMitglieder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParteiMitglieder_Parteien_ParteiId",
                        column: x => x.ParteiId,
                        principalTable: "Parteien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParteiMitglieder_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ParteiAgenten_AgentId",
                table: "ParteiAgenten",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParteiAgenten_ParteiId_AgentId",
                table: "ParteiAgenten",
                columns: new[] { "ParteiId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parteien_Aktenzeichen",
                table: "Parteien",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parteien_IstVerschlusssache",
                table: "Parteien",
                column: "IstVerschlusssache");

            migrationBuilder.CreateIndex(
                name: "IX_Parteien_Name",
                table: "Parteien",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ParteiMitglieder_ParteiId_PersonId",
                table: "ParteiMitglieder",
                columns: new[] { "ParteiId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParteiMitglieder_PersonId",
                table: "ParteiMitglieder",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParteiAgenten");

            migrationBuilder.DropTable(
                name: "ParteiMitglieder");

            migrationBuilder.DropTable(
                name: "Parteien");
        }
    }
}
