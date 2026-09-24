using NOOSE_Website.Models.Factions;

namespace NOOSE_Website.Services;

/// <summary>What makes two drug routes the same route; one route belongs to exactly one faction.</summary>
public static class DrugRouteRules
{
    /// <summary>Comparison key: case, outer and doubled whitespace do not make a different route.</summary>
    public static string Key(string? designation)
        => string.Join(' ', (designation ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    /// <summary>The input rows with a designation, each route once (the first entry wins).</summary>
    public static List<StockInput> Distinct(IEnumerable<StockInput> routes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return routes
            .Where(r => !string.IsNullOrWhiteSpace(r.Designation) && seen.Add(Key(r.Designation)))
            .ToList();
    }
}
