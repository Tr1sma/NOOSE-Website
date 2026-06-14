namespace NOOSE_Website.Models.Abstractions;

/// <summary>
/// Markiert eine Entität, deren Erstellungs-/Änderungs-Metadaten automatisch vom
/// <c>AuditSaveChangesInterceptor</c> gestempelt werden ("zuletzt aktualisiert von/am").
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    string? CreatedById { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedById { get; set; }
}
