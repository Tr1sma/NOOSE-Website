using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich10_TicketBeteiligteUndIntern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BuergerProfilId",
                table: "Tickets",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EroeffnetVonAgentId",
                table: "Tickets",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TicketBeteiligte",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TicketId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AgentId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZuletztGelesenAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketBeteiligte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketBeteiligte_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketBeteiligte_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Art_Status_LetzteAktivitaetAm",
                table: "Tickets",
                columns: new[] { "Art", "Status", "LetzteAktivitaetAm" });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_EroeffnetVonAgentId",
                table: "Tickets",
                column: "EroeffnetVonAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketBeteiligte_AgentId",
                table: "TicketBeteiligte",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketBeteiligte_TicketId_AgentId",
                table: "TicketBeteiligte",
                columns: new[] { "TicketId", "AgentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_EroeffnetVonAgentId",
                table: "Tickets",
                column: "EroeffnetVonAgentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_EroeffnetVonAgentId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketBeteiligte");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_Art_Status_LetzteAktivitaetAm",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_EroeffnetVonAgentId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EroeffnetVonAgentId",
                table: "Tickets");

            migrationBuilder.UpdateData(
                table: "Tickets",
                keyColumn: "BuergerProfilId",
                keyValue: null,
                column: "BuergerProfilId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "BuergerProfilId",
                table: "Tickets",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
