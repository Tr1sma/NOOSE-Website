using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Operationen;

/// <summary>
/// Eine Operation / ein Einsatzbericht als vollwertige Akte – Phase 5b. Eigenständige Ereignis-Akte (keine
/// Organisation mit Mitgliedern): bündelt Titel, Typ/Kategorie, Status, Ort und Zeitraum (<see cref="Beginn"/>/
/// <see cref="Ende"/>), den <see cref="Ablauf"/> und das <see cref="Ergebnis"/> sowie eine Einstufung mit Verlauf.
/// Beteiligte Agents laufen über die Join-Tabelle <see cref="OperationAgent"/>; beteiligte Personen und
/// Organisationen über die generische Verknüpfungs-Engine. Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
public class Operation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-OP-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Titel { get; set; } = string.Empty;

    /// <summary>Typ/Kategorie der Operation (z. B. Razzia, Observation) – Freitext mit Vorschlägen.</summary>
    public string? Typ { get; set; }

    public OperationStatus Status { get; set; } = OperationStatus.Geplant;

    /// <summary>Einsatzort (Freitext).</summary>
    public string? Ort { get; set; }

    /// <summary>Beginn des Einsatzzeitraums.</summary>
    public DateTime? Beginn { get; set; }

    /// <summary>Ende des Einsatzzeitraums.</summary>
    public DateTime? Ende { get; set; }

    /// <summary>Ablauf/Verlauf des Einsatzes (Freitext).</summary>
    public string? Ablauf { get; set; }

    /// <summary>Ergebnis/Ausgang des Einsatzes (Freitext).</summary>
    public string? Ergebnis { get; set; }

    /// <summary>Interne Bemerkungen/Vermerke (Freitext, getrennt von Ablauf/Ergebnis).</summary>
    public string? Bemerkungen { get; set; }

    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<OperationAgent> Agenten { get; set; } = new();

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
