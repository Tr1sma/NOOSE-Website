using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19_BedrohungsScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BedrohungsDetailJson",
                table: "Fraktionen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "BedrohungsKonfidenz",
                table: "Fraktionen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScoreBerechnetAm",
                table: "Fraktionen",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BedrohungsDetailJson",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "BedrohungsKonfidenz",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "ScoreBerechnetAm",
                table: "Fraktionen");
        }
    }
}
