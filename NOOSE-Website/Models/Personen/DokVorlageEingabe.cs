using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Personen;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten einer Dok-Vorlage (Erfassungsmaske).</summary>
public class DokVorlageEingabe
{
    public string Name { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public bool IstAktiv { get; set; } = true;
    public int Sortierung { get; set; }

    // ---- Default-Werte für die Dok-Felder ----
    public string? StandardGrund { get; set; }
    public string? StandardFraktion { get; set; }
    public string? StandardErhalteneInformationen { get; set; }
    public bool StandardWahrheitsserum { get; set; }
    public MassnahmeAusgang StandardAusgang { get; set; } = MassnahmeAusgang.LaeuftNoch;
}
