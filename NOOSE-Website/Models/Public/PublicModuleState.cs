namespace NOOSE_Website.Models.Public;

/// <summary>Display grouping of the public nav; independent of the module's own key.</summary>
public enum PublicModuleGroup
{
    Fahndung = 0,
    Behoerde = 1,
    Service = 2,
}

/// <summary>Labels for <see cref="PublicModuleGroup"/>.</summary>
public static class PublicModuleGroupDisplay
{
    public static string Name(PublicModuleGroup group) => group switch
    {
        PublicModuleGroup.Fahndung => "Fahndung",
        PublicModuleGroup.Behoerde => "Behörde",
        PublicModuleGroup.Service => "Service",
        _ => "—",
    };
}

/// <summary>Code-side definition of one public module; the switch row only carries the operator's choices.</summary>
/// <param name="Key">Stable key, also the row key in the database.</param>
/// <param name="Label">Default nav label.</param>
/// <param name="Description">What the module exposes publicly; shown in the settings panel.</param>
/// <param name="Icon">Default MudBlazor icon.</param>
/// <param name="NavRoute">Route of the module's own nav tab, or null when it has none.</param>
/// <param name="DefaultEnabled">State a freshly seeded row starts in.</param>
/// <param name="Available">Whether the pages behind the key already exist in this build.</param>
public sealed record PublicModuleDefinition(
    string Key,
    string Label,
    string Description,
    string Icon,
    string? NavRoute,
    PublicModuleGroup Group,
    int SortOrder,
    bool DefaultEnabled,
    bool Available,
    string DefaultOfflineText);

/// <summary>Catalog definition merged with the stored row; <see cref="IsEnabled"/> ignores the kill switch.</summary>
/// <remarks>
/// Carries both the effective values (<see cref="Label"/>, <see cref="Icon"/>, <see cref="OfflineText"/>) and the raw
/// overrides behind them. The settings panel needs the raw ones: an edit field pre-filled with a merged default would
/// turn every default into a stored override the first time anyone saves.
/// </remarks>
public sealed record PublicModuleState(
    string Key,
    string Label,
    string Description,
    string Icon,
    string? NavRoute,
    PublicModuleGroup Group,
    int SortOrder,
    bool IsEnabled,
    string OfflineText,
    bool Available,
    string? LabelOverride = null,
    string? IconOverride = null,
    string? OfflineTextOverride = null)
{
    public bool HasNavEntry => !string.IsNullOrWhiteSpace(NavRoute);
}

/// <summary>Cached read snapshot of the whole public area.</summary>
/// <remarks>
/// <see cref="PublicModuleState.IsEnabled"/> is the operator's switch alone so the settings panel can show what was
/// saved; every consumer asks <see cref="IsEnabled(string)"/>, which folds the kill switch in. Keeping both apart is
/// what lets the kill switch shut the area down without overwriting anyone's choices.
/// </remarks>
public sealed record PublicModuleSnapshot(bool KillSwitchActive, IReadOnlyList<PublicModuleState> Modules)
{
    public PublicModuleState? Find(string key)
        => Modules.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.Ordinal));

    /// <summary>Effective state: off while the kill switch is on, and an unknown key is never enabled.</summary>
    public bool IsEnabled(string key) => !KillSwitchActive && (Find(key)?.IsEnabled ?? false);

    /// <summary>Nav tabs of enabled, built modules that have a route, in display order.</summary>
    /// <remarks>
    /// <c>Available</c> is filtered here as well: a module may be switched on before its pages exist (that is what
    /// pre-configuring is for), and a tab pointing at a route that answers 404 would be worse than no tab. The stored
    /// choice survives, so the tab appears by itself once the phase that builds the pages flips <c>Available</c>.
    /// </remarks>
    public IReadOnlyList<PublicModuleState> NavEntries()
        => KillSwitchActive
            ? Array.Empty<PublicModuleState>()
            // group-major: the tab bar draws a separator between groups, so ordering by SortOrder alone let an
            // admin-set order interleave them and the separator then appeared mid-group
            : Modules.Where(m => m.IsEnabled && m.Available && m.HasNavEntry)
                .OrderBy(m => m.Group)
                .ThenBy(m => m.SortOrder)
                .ThenBy(m => m.Label, StringComparer.CurrentCulture)
                .ToList();
}

/// <summary>Input row of the settings panel.</summary>
public class PublicModuleInput
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? OfflineText { get; set; }
    public int SortOrder { get; set; }
    public string? LabelOverride { get; set; }
    public string? IconOverride { get; set; }
}
