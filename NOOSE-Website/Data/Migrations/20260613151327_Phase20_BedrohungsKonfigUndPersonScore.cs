using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase20_BedrohungsKonfigUndPersonScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BedrohungsDetailJson",
                table: "Personen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "BedrohungsKonfidenz",
                table: "Personen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BedrohungsScore",
                table: "Personen",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScoreBerechnetAm",
                table: "Personen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BedrohungsScoreKonfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Json = table.Column<string>(type: "longtext", nullable: true)
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
                    table.PrimaryKey("PK_BedrohungsScoreKonfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BedrohungsScoreKonfigs");

            migrationBuilder.DropColumn(
                name: "BedrohungsDetailJson",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "BedrohungsKonfidenz",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "BedrohungsScore",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "ScoreBerechnetAm",
                table: "Personen");
        }
    }
}
