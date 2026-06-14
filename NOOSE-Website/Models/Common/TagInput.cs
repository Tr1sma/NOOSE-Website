namespace NOOSE_Website.Models.Common;

/// <summary>Eingabemodell zum Anlegen/Bearbeiten eines Tags (Name + optionale Farbe).</summary>
public class TagInput
{
    public string Name { get; set; } = string.Empty;

    /// <summary>MudBlazor-Color-Name (z. B. „Primary"); leer = Standardfarbe.</summary>
    public string? Colour { get; set; }
}
