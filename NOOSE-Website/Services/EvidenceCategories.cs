namespace NOOSE_Website.Services;

/// <summary>Category filter sentinel and display label for evidence items.</summary>
public static class EvidenceCategories
{
    /// <summary>Filter value meaning "no category"; impossible as a real value because catalog values are trimmed non-empty.</summary>
    public const string None = "";

    /// <summary>German label for an item's category.</summary>
    public static string Label(string? category)
        => string.IsNullOrWhiteSpace(category) ? "Ohne Kategorie" : category;

    /// <summary>True when the filter asks for items without a category.</summary>
    public static bool IsNone(string? filter) => filter is { Length: 0 };

    /// <summary>In-memory twin of the SQL filter; case-insensitive because MySQL's default collation is.</summary>
    public static bool Matches(string? itemCategory, string? filter)
        => filter is null
           || (IsNone(filter)
               ? string.IsNullOrWhiteSpace(itemCategory)
               : string.Equals(itemCategory, filter, StringComparison.OrdinalIgnoreCase));
}
