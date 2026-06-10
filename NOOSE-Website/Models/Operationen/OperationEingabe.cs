using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Operationen;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten einer Operation/eines Einsatzberichts.</summary>
public class OperationEingabe
{
    public string Titel { get; set; } = string.Empty;
    public string? Typ { get; set; }
    public OperationStatus Status { get; set; } = OperationStatus.Geplant;
    public string? Ort { get; set; }
    public DateTime? Beginn { get; set; }
    public DateTime? Ende { get; set; }
    public string? Ablauf { get; set; }
    public string? Ergebnis { get; set; }
    public string? Bemerkungen { get; set; }
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;
    public string? EinstufungBegruendung { get; set; }
    public bool IstVerschlusssache { get; set; }
}
