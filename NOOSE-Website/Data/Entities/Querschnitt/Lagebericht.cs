using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

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
[Table("Lageberichte")]
public class Lagebericht : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Kalenderjahr des Berichtsmonats.</summary>
    [Column("Jahr")]
    public int Jahr { get; set; }

    /// <summary>Kalendermonat des Berichts (1–12).</summary>
    [Column("Monat")]
    public int Monat { get; set; }

    /// <summary>Anzeigetitel, z. B. „Lagebericht Juni 2026".</summary>
    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>
    /// Der zur Erzeugung serialisierte <c>StatistikReport</c> (JSON, longtext) – der eingefrorene Stand.
    /// </summary>
    public string SnapshotJson { get; set; } = string.Empty;

    // ---- IAuditable ----
    // ErstelltAm ist zugleich der „Berichtsstand" (Zeitpunkt der Erzeugung); ErstelltVonId ist der auslösende
    // Agent bei manueller Erzeugung bzw. null beim automatischen Hintergrund-Dienst.
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
