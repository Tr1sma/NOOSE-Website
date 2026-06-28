using System.Security.Claims;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Manages recency-light settings per record type and assesses records by last-modified date. Settings are cached so lists compute the light without a DB hit per row.</summary>
public interface IRecencyService
{
    /// <summary>Supported record types (display name + defaults) for the admin area.</summary>
    IReadOnlyList<RecencyTypeInfo> SupportedTypes { get; }

    /// <summary>Current settings per record type (code defaults overridden by stored values). Cached; one entry per supported type.</summary>
    Task<IReadOnlyDictionary<string, RecencySettings>> GetAllSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Settings for a single record type (default if not overridden).</summary>
    Task<RecencySettings> GetSettingsAsync(string recordsType, CancellationToken cancellationToken = default);

    /// <summary>Assesses a record by its reference date (modified-at ?? created-at, UTC); always Fresh when the type's aging is disabled.</summary>
    Task<RecencyLevel> AssessAsync(string recordsType, DateTime referenceDate, CancellationToken cancellationToken = default);

    /// <summary>Saves a record type's thresholds and per-type aging switch (leadership only) and clears the cache.</summary>
    Task SaveAsync(string recordsType, int warningDays, int staleDays, bool agingDisabled, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Toggles one record's aging exemption (leadership only); bypasses audit so it never resets the freshness signal.</summary>
    Task SetRecordExemptionAsync(string recordsType, string id, bool disabled, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <summary>A supported recency-light record type incl. display name and default thresholds.</summary>
public sealed record RecencyTypeInfo(string Type, string Display, int DefaultWarningDays, int DefaultStaleDays);

/// <summary>Resolved recency settings for a record type: thresholds plus the per-type aging switch.</summary>
public sealed record RecencySettings(int WarningDays, int StaleDays, bool AgingDisabled);
