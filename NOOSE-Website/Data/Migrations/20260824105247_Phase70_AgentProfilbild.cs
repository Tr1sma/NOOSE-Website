using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase70_AgentProfilbild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AusstehendesProfilbild",
                table: "AspNetUsers",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AusstehendesProfilbildTyp",
                table: "AspNetUsers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Profilbild",
                table: "AspNetUsers",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfilbildBeantragtAm",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilbildTyp",
                table: "AspNetUsers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AusstehendesProfilbild",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AusstehendesProfilbildTyp",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Profilbild",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilbildBeantragtAm",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilbildTyp",
                table: "AspNetUsers");
        }
    }
}
