using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>Formular-/Eingabemodell zum Anlegen eines Personen-Doks (Verhör/Maßnahme).</summary>
public class PersonDocInput
{
    /// <summary>Zeitpunkt der Maßnahme (RP-Zeit). Bei „Erschossen" Basis für das 20-Minuten-Tot-Fenster.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    /// <summary>Fraktionszugehörigkeit als Freitext – Rückfallebene, wenn keine Akte verknüpft ist.</summary>
    public string? Faction { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c>/<c>nameof(Personengruppe)</c> oder null.</summary>
    public string? OrgType { get; set; }

    /// <summary>Id der verknüpften Fraktion/Personengruppe oder null (dann zählt der Freitext).</summary>
    public string? OrgId { get; set; }

    /// <summary>Nur Eingabe-Steuerung (nicht persistiert): Person zugleich als Mitglied der verknüpften
    /// Organisation eintragen. Wirkt nur, wenn eine Akte verknüpft ist.</summary>
    public bool AsMember { get; set; }

    public string? ReceivedInformation { get; set; }
    public bool TruthSerum { get; set; }
    public MeasureOutcome Outcome { get; set; } = MeasureOutcome.RunningStill;
}
