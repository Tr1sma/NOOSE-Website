using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Ein Eintrag im Einstufungs-Verlauf einer Akte (append-only Historie). Polymorph über
/// <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> (wie Audit-/Zugriffs-Log und die übrigen
/// Querschnitts-Assoziationen) – gemeinsam genutzt von Person, Fraktion und Personengruppe.
/// </summary>
public class EinstufungVerlauf
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>CLR-Typname der Akte (z. B. <c>nameof(Person)</c>, <c>nameof(Fraktion)</c>).</summary>
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Id der zugehörigen Akte.</summary>
    public string EntitaetId { get; set; } = string.Empty;

    public Einstufung Wert { get; set; }
    public string? Begruendung { get; set; }
    public DateTime Zeitpunkt { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }

    /// <summary>Platzhalter für den späteren Antrags-Bezug (Phase 5).</summary>
    public string? AntragId { get; set; }
}
