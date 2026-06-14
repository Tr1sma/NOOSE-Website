using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Operationen;

/// <summary>
/// Eine Operation / ein Einsatzbericht als vollwertige Akte – Phase 5b. Eigenständige Ereignis-Akte (keine
/// Organisation mit Mitgliedern): bündelt Titel, Typ/Kategorie, Status, Ort und Zeitraum (<see cref="Beginn"/>/
/// <see cref="Ende"/>), den <see cref="Ablauf"/> und das <see cref="Ergebnis"/> sowie eine Einstufung mit Verlauf.
/// Beteiligte Agents laufen über die Join-Tabelle <see cref="OperationAgent"/>; beteiligte Personen und
/// Organisationen über die generische Verknüpfungs-Engine. Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
[Table("Operationen")]
public class Operation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-OP-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Typ/Kategorie der Operation (z. B. Razzia, Observation) – Freitext mit Vorschlägen.</summary>
    [Column("Typ")]
    public string? Typ { get; set; }

    public OperationStatus Status { get; set; } = OperationStatus.Geplant;

    /// <summary>Einsatzort (Freitext).</summary>
    [Column("Ort")]
    public string? Ort { get; set; }

    /// <summary>Beginn des Einsatzzeitraums.</summary>
    [Column("Beginn")]
    public DateTime? Beginn { get; set; }

    /// <summary>Ende des Einsatzzeitraums.</summary>
    [Column("Ende")]
    public DateTime? Ende { get; set; }

    /// <summary>Ablauf/Verlauf des Einsatzes (Freitext).</summary>
    [Column("Ablauf")]
    public string? Ablauf { get; set; }

    /// <summary>Ergebnis/Ausgang des Einsatzes (Freitext).</summary>
    [Column("Ergebnis")]
    public string? Ergebnis { get; set; }

    /// <summary>Interne Bemerkungen/Vermerke (Freitext, getrennt von Ablauf/Ergebnis).</summary>
    [Column("Bemerkungen")]
    public string? Bemerkungen { get; set; }

    [Column("Einstufung")]
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<OperationAgent> Agenten { get; set; } = new();

    // ---- IAuditable ----
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
