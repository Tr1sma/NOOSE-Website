using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Manages recency-light thresholds per record type and assesses records by last-modified date. Thresholds are cached so lists compute the light without a DB hit per row.</summary>
public interface IRecencyService
{
    /// <summary>Supported record types (display name + defaults) for the admin area.</summary>
    IReadOnlyList<RecencyTypeInfo> SupportedTypes { get; }

    /// <summary>Current thresholds per record type (code defaults overridden by stored values). Cached; one entry per supported type.</summary>
    Task<IReadOnlyDictionary<string, (int WarningDays, int StaleDays)>> GetThresholdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Thresholds for a single record type (default if not overridden).</summary>
    Task<(int WarningDays, int StaleDays)> GetThresholdAsync(string recordsType, CancellationToken cancellationToken = default);

    /// <summary>Assesses a record by its reference date (modified-at ?? created-at, UTC).</summary>
    Task<RecencyLevel> AssessAsync(string recordsType, DateTime referenceDate, CancellationToken cancellationToken = default);

    /// <summary>Saves a record type's thresholds (leadership only) and clears the cache.</summary>
    Task SaveAsync(string recordsType, int warningDays, int staleDays, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <summary>A supported recency-light record type incl. display name and default thresholds.</summary>
public sealed record RecencyTypeInfo(string Type, string Display, int DefaultWarningDays, int DefaultStaleDays);
