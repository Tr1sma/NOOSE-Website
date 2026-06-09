using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4e_NamensaenderungAusstehend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AusstehendeDienstnummer",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AusstehenderCodename",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AusstehenderKlarname",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "NamensaenderungBeantragtAm",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AusstehendeDienstnummer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AusstehenderCodename",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AusstehenderKlarname",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NamensaenderungBeantragtAm",
                table: "AspNetUsers");
        }
    }
}
