namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Jahres-Zähler für die fortlaufende Aktenzeichen-Vergabe (NOOSE-{Praefix}-{Jahr}-{Nummer}).
/// Wird beim Anlegen einer Akte atomar per „INSERT ... ON DUPLICATE KEY UPDATE" hochgezählt.
/// Pro Aktentyp eine eigene Sequenz über den <see cref="Praefix"/> (P = Person, F = Fraktion, G = Gruppe).
/// </summary>
public class AktenzeichenZaehler
{
    /// <summary>Präfix des Aktentyps (Teil des zusammengesetzten Primärschlüssels): P/F/G/…</summary>
    public string Praefix { get; set; } = "P";

    /// <summary>Kalenderjahr (Teil des zusammengesetzten Primärschlüssels).</summary>
    public int Jahr { get; set; }

    /// <summary>Zuletzt vergebene laufende Nummer in diesem Jahr für diesen Präfix.</summary>
    public int LetzteNummer { get; set; }
}
