namespace NOOSE_Website.Models.Factions;

/// <summary>A route the faction wants that another active faction holds.</summary>
/// <param name="Key">Comparison key (<c>DrugRouteRules.Key</c>); what a confirmation names.</param>
/// <param name="Designation">The route as the holder wrote it.</param>
/// <param name="HolderId">The holding faction; null when the actor may not see it.</param>
/// <param name="HolderName">Its name; null when the actor may not see it.</param>
/// <param name="MayTakeOver">The actor may see and edit the holder, so the route can move.</param>
public sealed record DrugRouteConflict(string Key, string Designation, string? HolderId, string? HolderName, bool MayTakeOver);

/// <summary>Saving would leave a route with two factions; nothing was written.</summary>
public sealed class DrugRouteConflictException(IReadOnlyList<DrugRouteConflict> conflicts)
    : InvalidOperationException(Describe(conflicts))
{
    public IReadOnlyList<DrugRouteConflict> Conflicts { get; } = conflicts;

    private static string Describe(IReadOnlyList<DrugRouteConflict> conflicts)
        => string.Join(" ", conflicts.Select(c => c.HolderName is { } name
            ? $"Die Route „{c.Designation}“ gehört bereits der Fraktion „{name}“."
            : $"Die Route „{c.Designation}“ gehört bereits einer Fraktion, die du nicht einsehen kannst."));
}
