using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich10_Tickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Aktenzeichen = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Art = table.Column<int>(type: "int", nullable: false),
                    BuergerProfilId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Betreff = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    BearbeiterId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LetzteAktivitaetAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ZuletztGelesenBuergerAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ZuletztGelesenAgentAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeschlossenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeschlossenVonId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_AspNetUsers_BearbeiterId",
                        column: x => x.BearbeiterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_BuergerProfile_BuergerProfilId",
                        column: x => x.BuergerProfilId,
                        principalTable: "BuergerProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TicketNachrichten",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TicketId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Zielgruppe = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VonBuerger = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AutorAgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                    table.PrimaryKey("PK_TicketNachrichten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketNachrichten_AspNetUsers_AutorAgentId",
                        column: x => x.AutorAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketNachrichten_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TicketNachrichten_AutorAgentId",
                table: "TicketNachrichten",
                column: "AutorAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketNachrichten_TicketId_ErstelltAm",
                table: "TicketNachrichten",
                columns: new[] { "TicketId", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketNachrichten_TicketId_Zielgruppe",
                table: "TicketNachrichten",
                columns: new[] { "TicketId", "Zielgruppe" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Aktenzeichen",
                table: "Tickets",
                column: "Aktenzeichen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BearbeiterId",
                table: "Tickets",
                column: "BearbeiterId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BuergerProfilId_ErstelltAm",
                table: "Tickets",
                columns: new[] { "BuergerProfilId", "ErstelltAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_BuergerProfilId_Status",
                table: "Tickets",
                columns: new[] { "BuergerProfilId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status_LetzteAktivitaetAm",
                table: "Tickets",
                columns: new[] { "Status", "LetzteAktivitaetAm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketNachrichten");

            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}
