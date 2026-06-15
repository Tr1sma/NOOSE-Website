namespace NOOSE_Website.Models.Abstractions;

/// <summary>
/// Markiert eine Entität für den Papierkorb: statt physisch gelöscht zu werden, wird sie
/// nur als gelöscht markiert (durch den <c>AuditSaveChangesInterceptor</c>) und von einem
/// globalen Query-Filter standardmäßig ausgeblendet. Wiederherstellbar durch Führung.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedById { get; set; }
}
