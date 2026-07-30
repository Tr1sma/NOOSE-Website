namespace NOOSE_Website.Models.Enums;

/// <summary>DB-backed display-name overrides for code-defined value lists; loaded once at startup and refreshed on admin edits. Swap is atomic via an immutable dictionary.</summary>
public static class EnumLabelText
{
    private static IReadOnlyDictionary<string, string> _labels = new Dictionary<string, string>();

    /// <summary>Overridden label for (list, enum-member key), or null when the code default applies.</summary>
    public static string? Get(string list, string key)
        => _labels.TryGetValue(Compose(list, key), out var label) ? label : null;

    /// <summary>Replaces all overrides atomically (startup load + after admin edits).</summary>
    public static void ReplaceAll(IEnumerable<(string List, string Key, string Label)> rows)
        => _labels = rows.ToDictionary(r => Compose(r.List, r.Key), r => r.Label);

    private static string Compose(string list, string key) => list + ":" + key;
}
