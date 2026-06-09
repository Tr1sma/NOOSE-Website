using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Personen;

/// <summary>Formular-/Eingabemodell zum Anlegen eines Personen-Doks (Verhör/Maßnahme).</summary>
public class PersonDokEingabe
{
    /// <summary>Zeitpunkt der Maßnahme (RP-Zeit). Bei „Erschossen" Basis für das 20-Minuten-Tot-Fenster.</summary>
    public DateTime Zeitpunkt { get; set; } = DateTime.UtcNow;
    public string? Grund { get; set; }
    public string? Fraktion { get; set; }
    public string? ErhalteneInformationen { get; set; }
    public bool Wahrheitsserum { get; set; }
    public MassnahmeAusgang Ausgang { get; set; } = MassnahmeAusgang.LaeuftNoch;
}
