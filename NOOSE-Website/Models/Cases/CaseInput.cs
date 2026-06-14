using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Cases;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Vorgangs-/Fallakte.</summary>
public class CaseInput
{
    public string Title { get; set; } = string.Empty;
    public string? Type { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public string? Description { get; set; }
    public string? Summary { get; set; }
    public string? ClosingNote { get; set; }
    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public bool IsClassified { get; set; }
}
