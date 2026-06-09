namespace NOOSE_Website.Models.Abstractions;

/// <summary>
/// Markiert eine Entitaet fuer den Papierkorb: statt physisch geloescht zu werden, wird sie
/// nur als geloescht markiert (durch den <c>AuditSaveChangesInterceptor</c>) und von einem
/// globalen Query-Filter standardmaessig ausgeblendet. Wiederherstellbar durch Fuehrung.
/// </summary>
public interface ISoftDelete
{
    bool IstGeloescht { get; set; }
    DateTime? GeloeschtAm { get; set; }
    string? GeloeschtVonId { get; set; }
}
