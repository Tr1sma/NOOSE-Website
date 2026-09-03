using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich07_HinweisLesestandUndPrio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrioritaetManuell",
                table: "Hinweise",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrioritaetManuellGrund",
                table: "Hinweise",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ZuletztGelesenAgentAm",
                table: "Hinweise",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrioritaetManuell",
                table: "Hinweise");

            migrationBuilder.DropColumn(
                name: "PrioritaetManuellGrund",
                table: "Hinweise");

            migrationBuilder.DropColumn(
                name: "ZuletztGelesenAgentAm",
                table: "Hinweise");
        }
    }
}
