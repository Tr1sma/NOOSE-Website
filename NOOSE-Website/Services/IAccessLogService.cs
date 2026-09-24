namespace NOOSE_Website.Services;

/// <summary>Logs who viewed which (sensitive) record and when.</summary>
public interface IAccessLogService
{
    Task LogViewAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Last view of the current agent's previous visit to the record; null on a first visit or for the demo visitor.</summary>
    Task<DateTime?> PreviousVisitAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}
