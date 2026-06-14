using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Jahres-Zähler für die fortlaufende Aktenzeichen-Vergabe (NOOSE-{Praefix}-{Jahr}-{Nummer}).
/// Wird beim Anlegen einer Akte atomar per „INSERT ... ON DUPLICATE KEY UPDATE" hochgezählt.
/// Pro Aktentyp eine eigene Sequenz über den <see cref="Praefix"/> (P = Person, F = Fraktion, G = Gruppe).
/// </summary>
[Table("AktenzeichenZaehler")]
public class CaseNumberCounter
{
    /// <summary>Präfix des Aktentyps (Teil des zusammengesetzten Primärschlüssels): P/F/G/…</summary>
    [Column("Praefix")]
    public string Prefix { get; set; } = "P";

    /// <summary>Kalenderjahr (Teil des zusammengesetzten Primärschlüssels).</summary>
    [Column("Jahr")]
    public int Year { get; set; }

    /// <summary>Zuletzt vergebene laufende Nummer in diesem Jahr für diesen Präfix.</summary>
    [Column("LetzteNummer")]
    public int LastNumber { get; set; }
}
