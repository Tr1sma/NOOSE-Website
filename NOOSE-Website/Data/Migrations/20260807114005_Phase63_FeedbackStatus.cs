using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase63_FeedbackStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Antwort",
                table: "FeedbackMeldungen",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EntscheiderName",
                table: "FeedbackMeldungen",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "EntschiedenAm",
                table: "FeedbackMeldungen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FeedbackMeldungen",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Antwort",
                table: "FeedbackMeldungen");

            migrationBuilder.DropColumn(
                name: "EntscheiderName",
                table: "FeedbackMeldungen");

            migrationBuilder.DropColumn(
                name: "EntschiedenAm",
                table: "FeedbackMeldungen");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FeedbackMeldungen");
        }
    }
}
