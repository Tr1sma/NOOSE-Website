namespace NOOSE_Website.Models.Personen;

/// <summary>Formular-/Eingabemodell zum Anlegen bzw. Bearbeiten einer Observation (Überwachungseintrag).</summary>
public class ObservationEingabe
{
    /// <summary>Beginn des Beobachtungsfensters (RP-Zeit). Pflichtfeld.</summary>
    public DateTime Beginn { get; set; } = DateTime.UtcNow;

    /// <summary>Ende des Beobachtungsfensters (optional).</summary>
    public DateTime? Ende { get; set; }

    public string? Ort { get; set; }
    public string? Beobachtung { get; set; }
    public string? Ergebnis { get; set; }

    /// <summary>Beobachtender Agent (Identity-Agent-Id); Default = erfassender Nutzer, kann aber abweichen.</summary>
    public string? BeobachtenderAgentId { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c>/<c>nameof(Personengruppe)</c> oder null.</summary>
    public string? OrgTyp { get; set; }

    /// <summary>Id der verknüpften Fraktion/Personengruppe oder null (kein Freitext-Fallback bei Observationen).</summary>
    public string? OrgId { get; set; }
}
