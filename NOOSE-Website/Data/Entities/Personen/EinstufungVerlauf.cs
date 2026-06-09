using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Ein Eintrag im Einstufungs-Verlauf einer Person (append-only Historie).</summary>
public class EinstufungVerlauf
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public Einstufung Wert { get; set; }
    public string? Begruendung { get; set; }
    public DateTime Zeitpunkt { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }

    /// <summary>Platzhalter für den späteren Antrags-Bezug (Phase 5).</summary>
    public string? AntragId { get; set; }
}
