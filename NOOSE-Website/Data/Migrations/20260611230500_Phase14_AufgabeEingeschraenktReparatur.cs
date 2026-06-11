using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase14_AufgabeEingeschraenktReparatur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reparatur-Migration: Auf manchen Umgebungen (u. a. Produktiv) wurde die
            // Migration Phase12_AufgabeEingeschraenkt einst mit leerem Body deployt und
            // dadurch in __EFMigrationsHistory als "angewandt" eingetragen, OHNE die Spalte
            // Aufgaben.IstEingeschraenkt anzulegen. Eine geänderte Phase12 wird von
            // MigrateAsync nicht erneut ausgeführt – deshalb holt diese neue Migration die
            // Spalte + den Index idempotent nach. Auf Umgebungen, die die Spalte bereits
            // haben (z. B. lokale Dev-DB), ist sie ein No-Op.
            //
            // Umgesetzt über eine temporäre Prozedur (statt User-Variablen/PREPARE), weil die
            // Connection-Strings hier kein AllowUserVariables setzen. Jede Anweisung läuft
            // außerhalb der Migrations-Transaktion (suppressTransaction), da MySQL bei DDL
            // ohnehin implizit committet.
            migrationBuilder.Sql(
                "DROP PROCEDURE IF EXISTS `__noose_fix_aufgabe_eingeschraenkt`;",
                suppressTransaction: true);

            migrationBuilder.Sql(@"
CREATE PROCEDURE `__noose_fix_aufgabe_eingeschraenkt`()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'Aufgaben'
          AND COLUMN_NAME = 'IstEingeschraenkt'
    ) THEN
        ALTER TABLE `Aufgaben` ADD COLUMN `IstEingeschraenkt` tinyint(1) NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'Aufgaben'
          AND INDEX_NAME = 'IX_Aufgaben_IstEingeschraenkt'
    ) THEN
        CREATE INDEX `IX_Aufgaben_IstEingeschraenkt` ON `Aufgaben` (`IstEingeschraenkt`);
    END IF;
END",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CALL `__noose_fix_aufgabe_eingeschraenkt`();",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP PROCEDURE IF EXISTS `__noose_fix_aufgabe_eingeschraenkt`;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bewusst leer: Die Spalte Aufgaben.IstEingeschraenkt "gehört" konzeptionell zu
            // Phase12; ein Rollback dieser reinen Reparatur-Migration soll sie nicht entfernen.
        }
    }
}
