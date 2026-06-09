namespace NOOSE_Website.Models.Abstractions;

/// <summary>
/// Markiert eine Entität, deren Erstellungs-/Änderungs-Metadaten automatisch vom
/// <c>AuditSaveChangesInterceptor</c> gestempelt werden ("zuletzt aktualisiert von/am").
/// </summary>
public interface IAuditable
{
    DateTime ErstelltAm { get; set; }
    string? ErstelltVonId { get; set; }
    DateTime? GeaendertAm { get; set; }
    string? GeaendertVonId { get; set; }
}
