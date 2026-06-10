using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Aufgaben;

/// <summary>
/// Eine Aufgabe/To-Do – Phase 6. Vollwertige, verknüpfbare Akte (Team-Board: für alle aktiven Agenten sichtbar,
/// daher <b>ohne</b> Verschlusssache/Einstufung – anders als <see cref="Vorgaenge.Vorgang"/>). Kann an mehrere Agenten
/// zugewiesen werden (<see cref="AufgabeZuweisung"/>) und über die generische Verknüpfungs-Engine mit beliebigen Akten
/// verknüpft werden. Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// <c>ErstelltVonId</c> ist der Ersteller.
/// </summary>
public class Aufgabe : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-A-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Titel { get; set; } = string.Empty;

    /// <summary>Beschreibung/Worum geht es (Freitext).</summary>
    public string? Beschreibung { get; set; }

    public AufgabeStatus Status { get; set; } = AufgabeStatus.Offen;

    public AufgabePrioritaet Prioritaet { get; set; } = AufgabePrioritaet.Normal;

    /// <summary>Fälligkeitsdatum (optional). Überfällig = in der Vergangenheit bei noch offenem Status.</summary>
    public DateTime? Faelligkeit { get; set; }

    /// <summary>Zeitpunkt des Abschlusses – gesetzt, sobald der Status auf Erledigt/Abgebrochen wechselt.</summary>
    public DateTime? ErledigtAm { get; set; }

    // ---- Kind-Tabellen ----
    public List<AufgabeZuweisung> Zuweisungen { get; set; } = new();

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
