using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase42_PersonnelEntryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtFrei",
                table: "AgentVermerke",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Ausfuehrende",
                table: "AgentVermerke",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "Datum",
                table: "AgentVermerke",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // backfill: existing entries keep their creation date as the entry date
            migrationBuilder.Sql("UPDATE AgentVermerke SET Datum = ErstelltAm;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtFrei",
                table: "AgentVermerke");

            migrationBuilder.DropColumn(
                name: "Ausfuehrende",
                table: "AgentVermerke");

            migrationBuilder.DropColumn(
                name: "Datum",
                table: "AgentVermerke");
        }
    }
}
