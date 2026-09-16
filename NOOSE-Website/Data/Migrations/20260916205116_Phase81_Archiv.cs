using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase81_Archiv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Vorgaenge",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Vorgaenge",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Vorgaenge",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Vorgaenge",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Taskforces",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Taskforces",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Taskforces",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Taskforces",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Personengruppen",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Personengruppen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Personengruppen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Personengruppen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Personen",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Personen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Personen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Personen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Parteien",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Parteien",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Parteien",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Parteien",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Operationen",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Operationen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Operationen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Operationen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Archivgrund",
                table: "Fraktionen",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiviertAm",
                table: "Fraktionen",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiviertVonId",
                table: "Fraktionen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstArchiviert",
                table: "Fraktionen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Vorgaenge_IstArchiviert",
                table: "Vorgaenge",
                column: "IstArchiviert");

            migrationBuilder.CreateIndex(
                name: "IX_Taskforces_IstArchiviert",
                table: "Taskforces",
                column: "IstArchiviert");

            migrationBuilder.CreateIndex(
                name: "IX_Personengruppen_IstArchiviert",
                table: "Personengruppen",
                column: "IstArchiviert");

            migrationBuilder.CreateIndex(
                name: "IX_Personen_IstArchiviert",
                table: "Personen",
                column: "IstArchiviert");

            migrationBuilder.CreateIndex(
                name: "IX_Parteien_IstArchiviert",
                table: "Parteien",
                column: "IstArchiviert");

            migrationBuilder.CreateIndex(
                name: "IX_Operationen_IstArchiviert",
                table: "Operationen",
                column: "IstArchiviert");

            migrationBuilder.CreateIndex(
                name: "IX_Fraktionen_IstArchiviert",
                table: "Fraktionen",
                column: "IstArchiviert");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vorgaenge_IstArchiviert",
                table: "Vorgaenge");

            migrationBuilder.DropIndex(
                name: "IX_Taskforces_IstArchiviert",
                table: "Taskforces");

            migrationBuilder.DropIndex(
                name: "IX_Personengruppen_IstArchiviert",
                table: "Personengruppen");

            migrationBuilder.DropIndex(
                name: "IX_Personen_IstArchiviert",
                table: "Personen");

            migrationBuilder.DropIndex(
                name: "IX_Parteien_IstArchiviert",
                table: "Parteien");

            migrationBuilder.DropIndex(
                name: "IX_Operationen_IstArchiviert",
                table: "Operationen");

            migrationBuilder.DropIndex(
                name: "IX_Fraktionen_IstArchiviert",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Taskforces");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Taskforces");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Taskforces");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Taskforces");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "Archivgrund",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "ArchiviertAm",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "ArchiviertVonId",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "IstArchiviert",
                table: "Fraktionen");
        }
    }
}
