using NOOSE_Website.Models.Evidence;

namespace NOOSE_Website.Services;

/// <summary>Category group of the clearing dialog; the key is empty for uncategorised items.</summary>
public sealed record EvidenceClearingGroup(string Key, string Label, IReadOnlyList<EvidenceItemDisplay> Rows);

/// <summary>Groups clearing rows by category and describes what a selection covers.</summary>
public static class EvidenceClearingGroups
{
    /// <summary>Groups rows by category; named groups sort alphabetically, uncategorised comes last.</summary>
    public static IReadOnlyList<EvidenceClearingGroup> Build(IEnumerable<EvidenceItemDisplay> rows)
        => rows
            // case-insensitive like the SQL filter; the first spelling seen becomes the label
            .GroupBy(r => Key(r.Item.Category), StringComparer.OrdinalIgnoreCase)
            .Select(g => new EvidenceClearingGroup(
                g.Key,
                EvidenceCategories.Label(g.Key),
                g.ToList()))
            .OrderBy(g => EvidenceCategories.IsNone(g.Key) ? 1 : 0)
            .ThenBy(g => g.Label, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>Tri-state of a group: all rows selected, none, or null for a partial selection.</summary>
    public static bool? State(IReadOnlyList<EvidenceItemDisplay> rows, IReadOnlySet<string> selected)
    {
        if (rows.Count == 0)
        {
            return false;
        }
        var hit = rows.Count(r => selected.Contains(r.Item.Id));
        return hit == 0 ? false : hit == rows.Count ? true : null;
    }

    /// <summary>Describes the booked scope for the success message; whole categories are named, mixes are not.</summary>
    public static string ScopeLabel(IReadOnlyList<EvidenceClearingGroup> groups, IReadOnlySet<string> selected)
    {
        var full = new List<EvidenceClearingGroup>();
        foreach (var group in groups)
        {
            switch (State(group.Rows, selected))
            {
                case null:
                    // a half-cleared category must not be reported as cleared
                    return "Auswahl geräumt";
                case true:
                    full.Add(group);
                    break;
            }
        }
        if (full.Count == 0)
        {
            return "Auswahl geräumt";
        }
        if (full.Count == groups.Count)
        {
            return "Kammer geräumt";
        }
        return full.Count == 1
            ? $"„{full[0].Label}“ geräumt"
            : $"{full.Count} Kategorien geräumt";
    }

    /// <summary>Normalises a category to its group key; blank and null share the uncategorised group.</summary>
    private static string Key(string? category)
        => string.IsNullOrWhiteSpace(category) ? EvidenceCategories.None : category;
}
