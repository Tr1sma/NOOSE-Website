namespace NOOSE_Website.Models.Common;

/// <summary>Eingabe zum Anlegen einer generischen Verknüpfung auf eine beliebige Ziel-Akte.</summary>
public class LinkInput
{
    /// <summary>Aktentyp des Ziels (z. B. „Person", „Fraktion", „Agent", „PersonDok"). Standard: Person.</summary>
    public string TargetType { get; set; } = "Person";
    public string TargetId { get; set; } = string.Empty;
    public string? Label { get; set; }

    /// <summary>Nur-UI: lesbarer Anzeigename des Ziels (vom Picker gesetzt, nicht persistiert).</summary>
    public string? Display { get; set; }
}

/// <summary>Aufbereitete Verknüpfung aus Sicht einer Akte: die jeweils „andere Seite" samt Bezeichnung.</summary>
/// <param name="Href">Navigationsziel der anderen Seite (z. B. „/personen/{id}") oder null, wenn nicht navigierbar (z. B. Agent).</param>
public record LinkDisplay(
    string LinkId,
    string OtherType,
    string OtherId,
    string? Label,
    string OtherDesignation,
    bool Automatic = false,
    string? Href = null);
