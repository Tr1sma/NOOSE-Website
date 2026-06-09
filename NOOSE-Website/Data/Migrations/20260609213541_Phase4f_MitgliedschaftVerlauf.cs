using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4f_MitgliedschaftVerlauf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Der Unique-Index (OrgId, PersonId) ist zugleich der Backing-Index des FK auf die Eltern-Akte –
            // MySQL/MariaDB lässt ihn nicht droppen, solange der FK besteht. Daher: FK lösen, Index durch einen
            // nicht-eindeutigen ersetzen (Soft-Delete + Wiedereintritt verträgt keine Unique-Constraint), FK
            // wieder anlegen. Reihenfolge bewusst, da MySQL DDL nicht transaktional ist.
            migrationBuilder.DropForeignKey(
                name: "FK_PersonengruppeMitglieder_Personengruppen_PersonengruppeId",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropForeignKey(
                name: "FK_FraktionMitglieder_Fraktionen_FraktionId",
                table: "FraktionMitglieder");

            migrationBuilder.DropIndex(
                name: "IX_PersonengruppeMitglieder_PersonengruppeId_PersonId",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropIndex(
                name: "IX_FraktionMitglieder_FraktionId_PersonId",
                table: "FraktionMitglieder");

            // ---- ISoftDelete-Spalten (Mitgliedschafts-Verlauf): GeloeschtAm = Austrittsdatum ----
            migrationBuilder.AddColumn<DateTime>(
                name: "GeloeschtAm",
                table: "PersonengruppeMitglieder",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeloeschtVonId",
                table: "PersonengruppeMitglieder",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstGeloescht",
                table: "PersonengruppeMitglieder",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeloeschtAm",
                table: "FraktionMitglieder",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeloeschtVonId",
                table: "FraktionMitglieder",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IstGeloescht",
                table: "FraktionMitglieder",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Nicht-eindeutige Indizes (backen weiterhin die FKs auf die Eltern-Akte).
            migrationBuilder.CreateIndex(
                name: "IX_PersonengruppeMitglieder_PersonengruppeId_PersonId",
                table: "PersonengruppeMitglieder",
                columns: new[] { "PersonengruppeId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_FraktionMitglieder_FraktionId_PersonId",
                table: "FraktionMitglieder",
                columns: new[] { "FraktionId", "PersonId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PersonengruppeMitglieder_Personengruppen_PersonengruppeId",
                table: "PersonengruppeMitglieder",
                column: "PersonengruppeId",
                principalTable: "Personengruppen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FraktionMitglieder_Fraktionen_FraktionId",
                table: "FraktionMitglieder",
                column: "FraktionId",
                principalTable: "Fraktionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonengruppeMitglieder_Personengruppen_PersonengruppeId",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropForeignKey(
                name: "FK_FraktionMitglieder_Fraktionen_FraktionId",
                table: "FraktionMitglieder");

            migrationBuilder.DropIndex(
                name: "IX_PersonengruppeMitglieder_PersonengruppeId_PersonId",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropIndex(
                name: "IX_FraktionMitglieder_FraktionId_PersonId",
                table: "FraktionMitglieder");

            migrationBuilder.DropColumn(
                name: "GeloeschtAm",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropColumn(
                name: "GeloeschtVonId",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropColumn(
                name: "IstGeloescht",
                table: "PersonengruppeMitglieder");

            migrationBuilder.DropColumn(
                name: "GeloeschtAm",
                table: "FraktionMitglieder");

            migrationBuilder.DropColumn(
                name: "GeloeschtVonId",
                table: "FraktionMitglieder");

            migrationBuilder.DropColumn(
                name: "IstGeloescht",
                table: "FraktionMitglieder");

            migrationBuilder.CreateIndex(
                name: "IX_PersonengruppeMitglieder_PersonengruppeId_PersonId",
                table: "PersonengruppeMitglieder",
                columns: new[] { "PersonengruppeId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FraktionMitglieder_FraktionId_PersonId",
                table: "FraktionMitglieder",
                columns: new[] { "FraktionId", "PersonId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonengruppeMitglieder_Personengruppen_PersonengruppeId",
                table: "PersonengruppeMitglieder",
                column: "PersonengruppeId",
                principalTable: "Personengruppen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FraktionMitglieder_Fraktionen_FraktionId",
                table: "FraktionMitglieder",
                column: "FraktionId",
                principalTable: "Fraktionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
