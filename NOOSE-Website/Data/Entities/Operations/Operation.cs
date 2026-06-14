using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Operations;

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
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Typ/Kategorie der Operation (z. B. Razzia, Observation) – Freitext mit Vorschlägen.</summary>
    [Column("Typ")]
    public string? Type { get; set; }

    public OperationStatus Status { get; set; } = OperationStatus.Planned;

    /// <summary>Einsatzort (Freitext).</summary>
    [Column("Ort")]
    public string? Location { get; set; }

    /// <summary>Beginn des Einsatzzeitraums.</summary>
    [Column("Beginn")]
    public DateTime? Start { get; set; }

    /// <summary>Ende des Einsatzzeitraums.</summary>
    [Column("Ende")]
    public DateTime? End { get; set; }

    /// <summary>Ablauf/Verlauf des Einsatzes (Freitext).</summary>
    [Column("Ablauf")]
    public string? Expiry { get; set; }

    /// <summary>Ergebnis/Ausgang des Einsatzes (Freitext).</summary>
    [Column("Ergebnis")]
    public string? Result { get; set; }

    /// <summary>Interne Bemerkungen/Vermerke (Freitext, getrennt von Ablauf/Ergebnis).</summary>
    [Column("Bemerkungen")]
    public string? Remarks { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    // ---- Kind-Tabellen ----
    public List<OperationAgent> Agents { get; set; } = new();

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
