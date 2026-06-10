using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_PersonalakteUndObservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentBefoerderungsantraege",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZielDienstgrad = table.Column<int>(type: "int", nullable: false),
                    Begruendung = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AntragstellerName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntscheiderName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntschiedenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Entscheidungsnotiz = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
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
                    table.PrimaryKey("PK_AgentBefoerderungsantraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentBefoerderungsantraege_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgentDienstgradVerlaeufe",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Alt = table.Column<int>(type: "int", nullable: true),
                    Neu = table.Column<int>(type: "int", nullable: false),
                    Zeitpunkt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AkteurName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Grund = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentDienstgradVerlaeufe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentDienstgradVerlaeufe_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgentVermerke",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AutorName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
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
                    table.PrimaryKey("PK_AgentVermerke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentVermerke_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Observationen",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PersonId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beginn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Ende = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Ort = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beobachtung = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ergebnis = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeobachtenderAgentId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrgTyp = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrgId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_Observationen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Observationen_AspNetUsers_BeobachtenderAgentId",
                        column: x => x.BeobachtenderAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Observationen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AgentBefoerderungsantraege_AgentId_Status",
                table: "AgentBefoerderungsantraege",
                columns: new[] { "AgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentBefoerderungsantraege_Status",
                table: "AgentBefoerderungsantraege",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentDienstgradVerlaeufe_AgentId_Zeitpunkt",
                table: "AgentDienstgradVerlaeufe",
                columns: new[] { "AgentId", "Zeitpunkt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentVermerke_AgentId_Art",
                table: "AgentVermerke",
                columns: new[] { "AgentId", "Art" });

            migrationBuilder.CreateIndex(
                name: "IX_Observationen_Beginn",
                table: "Observationen",
                column: "Beginn");

            migrationBuilder.CreateIndex(
                name: "IX_Observationen_BeobachtenderAgentId",
                table: "Observationen",
                column: "BeobachtenderAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Observationen_OrgTyp_OrgId",
                table: "Observationen",
                columns: new[] { "OrgTyp", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Observationen_PersonId",
                table: "Observationen",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentBefoerderungsantraege");

            migrationBuilder.DropTable(
                name: "AgentDienstgradVerlaeufe");

            migrationBuilder.DropTable(
                name: "AgentVermerke");

            migrationBuilder.DropTable(
                name: "Observationen");
        }
    }
}
