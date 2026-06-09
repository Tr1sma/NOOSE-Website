namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Eingabemodell zum Anlegen/Bearbeiten eines Tags (Name + optionale Farbe).</summary>
public class TagEingabe
{
    public string Name { get; set; } = string.Empty;

    /// <summary>MudBlazor-Color-Name (z. B. „Primary"); leer = Standardfarbe.</summary>
    public string? Farbe { get; set; }
}
