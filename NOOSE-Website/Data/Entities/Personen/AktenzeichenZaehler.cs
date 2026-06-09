namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Jahres-Zähler für die fortlaufende Aktenzeichen-Vergabe (NOOSE-P-{Jahr}-{Nummer}).
/// Wird beim Anlegen einer Person atomar per „INSERT ... ON DUPLICATE KEY UPDATE" hochgezählt.
/// </summary>
public class AktenzeichenZaehler
{
    /// <summary>Kalenderjahr (Primärschlüssel).</summary>
    public int Jahr { get; set; }

    /// <summary>Zuletzt vergebene laufende Nummer in diesem Jahr.</summary>
    public int LetzteNummer { get; set; }
}
