using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Operations;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Operation/eines Einsatzberichts.</summary>
public class OperationInput
{
    public string Title { get; set; } = string.Empty;
    public string? Type { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Planned;
    public string? Location { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? Expiry { get; set; }
    public string? Result { get; set; }
    public string? Remarks { get; set; }
    public Classification Classification { get; set; } = Classification.Unknown;
    public string? ClassificationJustification { get; set; }
    public bool IsClassified { get; set; }
}
