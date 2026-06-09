namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Eingabe zum Anlegen einer generischen Verknüpfung auf eine Ziel-Akte (in Phase 3 stets eine Person).</summary>
public class VerknuepfungEingabe
{
    public string ZielId { get; set; } = string.Empty;
    public string? Label { get; set; }
}

/// <summary>Aufbereitete Verknüpfung aus Sicht einer Akte: die jeweils „andere Seite" samt Bezeichnung.</summary>
public record VerknuepfungAnzeige(
    string VerknuepfungId,
    string AndereTyp,
    string AndereId,
    string? Label,
    string AndereBezeichnung,
    bool Automatisch = false);
