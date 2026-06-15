namespace NOOSE_Website.Models.People;

/// <summary>Formular-/Eingabemodell zum Anlegen bzw. Bearbeiten einer Observation (Überwachungseintrag).</summary>
public class ObservationInput
{
    /// <summary>Beginn des Beobachtungsfensters (RP-Zeit). Pflichtfeld.</summary>
    public DateTime Start { get; set; } = DateTime.UtcNow;

    /// <summary>Ende des Beobachtungsfensters (optional).</summary>
    public DateTime? End { get; set; }

    public string? Location { get; set; }
    public string? Sighting { get; set; }
    public string? Result { get; set; }

    /// <summary>Beobachtender Agent (Identity-Agent-Id); Default = erfassender Nutzer, kann aber abweichen.</summary>
    public string? ObservingAgentId { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c>/<c>nameof(Personengruppe)</c> oder null.</summary>
    public string? OrgType { get; set; }

    /// <summary>Id der verknüpften Fraktion/Personengruppe oder null (kein Freitext-Fallback bei Observationen).</summary>
    public string? OrgId { get; set; }
}
