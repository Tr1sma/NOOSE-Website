namespace NOOSE_Website.Services;

/// <summary>Computes and persists the automatic threat score (EHK-Score, see AlgoPlan.md) via ExecuteUpdateAsync past the audit interceptor (no modified stamp, no audit-log flood). Called event-driven and from the nightly sweep.</summary>
public interface IThreatScoreService
{
    /// <summary>Recomputes and persists a faction's score. Idempotent; own DbContext. Deleted factions skipped, state factions set to null.</summary>
    Task NewCalculateAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes every faction the person was ever a member of; call after a change to their measure docs.</summary>
    Task NewCalculateForPersonAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes all non-deleted factions (nightly sweep against decay drift). Returns the count actually computed.</summary>
    Task<int> NewCalculateAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Recomputes and persists a person's score. Idempotent; own DbContext.</summary>
    Task NewCalculatePersonScoreAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>Recomputes all non-deleted persons (sweep against decay drift). Returns the count.</summary>
    Task<int> NewCalculateAllPeopleScoresAsync(CancellationToken cancellationToken = default);
}
