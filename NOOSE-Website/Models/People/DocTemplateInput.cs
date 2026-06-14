using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>Formular-/Eingabemodell zum Anlegen/Bearbeiten einer Dok-Vorlage (Erfassungsmaske).</summary>
public class DocTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }

    // ---- Default-Werte für die Dok-Felder ----
    public string? DefaultReason { get; set; }
    public string? DefaultFaction { get; set; }
    public string? DefaultReceivedInformation { get; set; }
    public bool DefaultTruthSerum { get; set; }
    public MeasureOutcome DefaultOutcome { get; set; } = MeasureOutcome.RunningStill;
}
