namespace NOOSE_Website.Services;

/// <summary>Logs who viewed which (sensitive) record and when.</summary>
public interface IAccessLogService
{
    Task LogViewAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}
