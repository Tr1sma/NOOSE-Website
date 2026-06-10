using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Vorgaenge;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Vorgangs-/Fallakte.</summary>
public class VorgangEingabe
{
    public string Titel { get; set; } = string.Empty;
    public string? Typ { get; set; }
    public VorgangStatus Status { get; set; } = VorgangStatus.Offen;
    public string? Beschreibung { get; set; }
    public string? Zusammenfassung { get; set; }
    public string? Abschlussvermerk { get; set; }
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public bool IstVerschlusssache { get; set; }
}
