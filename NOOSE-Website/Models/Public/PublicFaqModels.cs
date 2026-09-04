namespace NOOSE_Website.Models.Public;

/// <summary>One question as the outside world reads it.</summary>
/// <param name="Anchor">The fragment the question is addressed by; stored, so a shared link keeps working.</param>
/// <param name="PlainText">The answer without markup, produced once per cache fill for the public search.</param>
/// <param name="Hidden">Only ever true in the agent preview; the published snapshot leaves hidden rows out.</param>
public sealed record PublicFaqEntryView(
    string Anchor,
    string Question,
    string Html,
    string PlainText,
    bool Hidden = false);

/// <summary>One main section of the FAQ and the questions under it.</summary>
public sealed record PublicFaqRubrikView(
    string Title,
    string? Description,
    string Icon,
    bool DefaultOpen,
    IReadOnlyList<PublicFaqEntryView> Entries,
    bool Hidden = false)
{
    /// <summary>True when the section has to start expanded: by configuration, or because it holds the target.</summary>
    public bool OpensFor(string? anchor)
        => DefaultOpen
            || (!string.IsNullOrEmpty(anchor)
                && Entries.Any(e => string.Equals(e.Anchor, anchor, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>Everything the FAQ shows, in display order.</summary>
public sealed record PublicFaqSnapshot(IReadOnlyList<PublicFaqRubrikView> Rubriken)
{
    public static PublicFaqSnapshot Empty { get; } = new([]);

    public bool IsEmpty => Rubriken.Count == 0;

    /// <summary>Every question across all sections, flattened for the search.</summary>
    public IEnumerable<(PublicFaqRubrikView Rubrik, PublicFaqEntryView Entry)> All()
        => Rubriken.SelectMany(r => r.Entries.Select(e => (r, e)));
}

/// <summary>One question in the editorial panel; carries no answer.</summary>
/// <remarks>
/// No HTML, for the reason the editorial page row carries none: an answer may hold a pasted picture as base64, and
/// a list that attached every body would pull megabytes just to render a table of questions.
/// </remarks>
public sealed record PublicFaqEntryRow(
    string Id,
    string Question,
    string Anchor,
    int SortOrder,
    bool IsVisible,
    bool HasAnswer);

/// <summary>One section in the editorial panel.</summary>
public sealed record PublicFaqRubrikRow(
    string Id,
    string Title,
    string? Description,
    string? IconName,
    int SortOrder,
    bool IsVisible,
    bool DefaultOpen,
    IReadOnlyList<PublicFaqEntryRow> Entries);

/// <summary>What the editorial panel needs to draw itself.</summary>
/// <param name="PageIsPublished">The page the FAQ lives on is live; false means nothing of this is reachable.</param>
/// <param name="ModuleIsOn">The Information module is switched on; the second gate, reported separately so the
/// panel can name the one that is actually shut.</param>
public sealed record PublicFaqAdminView(
    bool PageIsPublished,
    bool ModuleIsOn,
    IReadOnlyList<PublicFaqRubrikRow> Rubriken)
{
    public static PublicFaqAdminView Empty { get; } = new(false, false, []);
}

/// <summary>Editorial input for a section.</summary>
public class PublicFaqRubrikInput
{
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool DefaultOpen { get; set; }
}

/// <summary>Editorial input for a question.</summary>
public class PublicFaqEntryInput
{
    public string? Id { get; set; }
    public string RubrikId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;

    /// <summary>Null leaves the stored answer alone; an empty string clears it.</summary>
    /// <remarks>
    /// The same split the editorial page draws: without it a caller that only renames a question would wipe the
    /// answer, and the loss would be silent.
    /// </remarks>
    public string? AnswerHtml { get; set; }

    public bool IsVisible { get; set; } = true;
}
