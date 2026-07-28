using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase43_AgentKuendigung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GekuendigtAm",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GekuendigtVonId",
                table: "AspNetUsers",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GekuendigtVonName",
                table: "AspNetUsers",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Kuendigungsgrund",
                table: "AspNetUsers",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GekuendigtAm",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GekuendigtVonId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GekuendigtVonName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Kuendigungsgrund",
                table: "AspNetUsers");
        }
    }
}
