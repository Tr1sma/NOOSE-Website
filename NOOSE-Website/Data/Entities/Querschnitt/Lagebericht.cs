using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein archivierter, automatisch erzeugter Monats-Lagebericht (Phase 8 / Block D, Schritt 2). Hält einen
/// eingefrorenen Schnappschuss der Statistik-Auswertungen (<c>StatistikReport</c> als JSON) zum Zeitpunkt der
/// Erzeugung – die Detailansicht rendert genau diesen Stand, nicht die aktuelle Lage. Identität ist der
/// Berichtsmonat (<see cref="Jahr"/>/<see cref="Monat"/>); je Monat existiert höchstens ein aktiver Bericht
/// (Eindeutigkeit prüft der Dienst per Aktiv-Abfrage – analog zu anderen soft-delete-fähigen Akten ohne
/// Unique-Index, damit ein neu erzeugter Bericht einen alten ersetzen kann). Voll auditiert und papierkorbfähig.
/// Der Bericht enthält die VOLLE Lage (inkl. Verschlusssachen-Aggregate) und ist daher Führung vorbehalten.
/// </summary>
public class Lagebericht : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Kalenderjahr des Berichtsmonats.</summary>
    public int Jahr { get; set; }

    /// <summary>Kalendermonat des Berichts (1–12).</summary>
    public int Monat { get; set; }

    /// <summary>Anzeigetitel, z. B. „Lagebericht Juni 2026".</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>
    /// Der zur Erzeugung serialisierte <c>StatistikReport</c> (JSON, longtext) – der eingefrorene Stand.
    /// </summary>
    public string SnapshotJson { get; set; } = string.Empty;

    // ---- IAuditable ----
    // ErstelltAm ist zugleich der „Berichtsstand" (Zeitpunkt der Erzeugung); ErstelltVonId ist der auslösende
    // Agent bei manueller Erzeugung bzw. null beim automatischen Hintergrund-Dienst.
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
