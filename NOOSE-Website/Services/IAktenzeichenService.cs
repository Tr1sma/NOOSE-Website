using NOOSE_Website.Data;

namespace NOOSE_Website.Services;

/// <summary>
/// Vergibt fortlaufende, menschenlesbare Aktenzeichen (NOOSE-{Praefix}-{Jahr}-{Nummer}) je Aktentyp.
/// Gemeinsam genutzt von Person (P), Fraktion (F) und Personengruppe (G).
/// </summary>
public interface IAktenzeichenService
{
    /// <summary>
    /// Erhöht den Jahres-Zähler des Präfixes race-sicher (auf dem <b>übergebenen</b> Kontext, damit der
    /// Inkrement in der Transaktion des Aufrufers bleibt) und liefert das formatierte Aktenzeichen.
    /// </summary>
    Task<string> NaechstesAsync(AppDbContext db, string praefix, CancellationToken cancellationToken = default);
}
