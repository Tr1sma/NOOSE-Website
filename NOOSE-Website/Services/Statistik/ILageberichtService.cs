using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Statistik;

namespace NOOSE_Website.Services.Statistik;

/// <summary>
/// Erzeugt, archiviert und liest die automatischen Monats-Lageberichte (Phase 8 / Block D, Schritt 2).
/// Ein Bericht friert den vollständigen <see cref="StatistikReport"/> (inkl. Verschlusssachen-Aggregate,
/// daher Führung vorbehalten) zum Erzeugungszeitpunkt als JSON ein. Je Berichtsmonat existiert höchstens
/// ein aktiver Bericht.
/// </summary>
public interface ILageberichtService
{
    /// <summary>
    /// Erzeugt den Lagebericht für den angegebenen Monat und persistiert ihn. Existiert bereits ein aktiver
    /// Bericht für den Monat: bei <paramref name="ersetzeVorhandene"/>=false wird <c>null</c> zurückgegeben
    /// (kein Überschreiben), sonst wird der alte per Soft-Delete ersetzt. Benachrichtigt die Führung
    /// (best-effort). <paramref name="ausloeserId"/> = manueller Auslöser bzw. <c>null</c> beim Dienst.
    /// </summary>
    Task<Lagebericht?> ErzeugeMonatAsync(int jahr, int monat, bool ersetzeVorhandene, string? ausloeserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Erzeugt den Bericht des zum Zeitpunkt <paramref name="jetztUtc"/> zuletzt abgeschlossenen Monats
    /// (Vormonat), falls dafür noch kein aktiver Bericht existiert. Für den Hintergrund-Dienst. Liefert
    /// <c>true</c>, wenn ein Bericht neu erzeugt wurde.
    /// </summary>
    Task<bool> ErzeugeFaelligenAsync(DateTime jetztUtc, CancellationToken cancellationToken = default);

    /// <summary>Alle archivierten Berichte als Kopfzeilen (neueste zuerst), ohne den JSON-Snapshot.</summary>
    Task<List<LageberichtKopf>> GetArchivAsync(CancellationToken cancellationToken = default);

    /// <summary>Lädt einen Bericht inkl. deserialisiertem Snapshot für die Detailansicht; <c>null</c> wenn nicht gefunden/lesbar.</summary>
    Task<LageberichtAnzeige?> GetAnzeigeAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Verschiebt einen Bericht in den Papierkorb (Soft-Delete).</summary>
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
