using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>Hub and menu entry of a published page.</summary>
public sealed record PublicPageLink(string Slug, string MenuTitle, string Icon, int SortOrder);

/// <summary>What an outside visitor gets to see of a page; the HTML is already sanitized.</summary>
/// <param name="IsDraft">True only on the agent-side preview, so the page can say so out loud.</param>
public sealed record PublicPageView(
    string Slug,
    string Title,
    string Html,
    DateTime? PublishedAt,
    bool IsDraft = false);

/// <summary>Cached read snapshot of everything published.</summary>
/// <remarks>
/// <see cref="Menu"/> is a subset of <see cref="Pages"/>: a published page that is not listed stays readable by
/// direct link. Reading both from one snapshot keeps the two from disagreeing within a cache window.
/// </remarks>
/// <param name="SearchText">
/// Body as plain text, per slug, computed once per cache fill - see PublicPressSnapshot for the reason.
/// </param>
public sealed record PublicPageSnapshot(
    IReadOnlyList<PublicPageLink> Menu,
    IReadOnlyDictionary<string, PublicPageView> Pages,
    IReadOnlyDictionary<string, string>? SearchText = null)
{
    public static PublicPageSnapshot Empty { get; } =
        new([], new Dictionary<string, PublicPageView>(StringComparer.OrdinalIgnoreCase));

    public PublicPageView? Find(string? slug)
        => slug is not null && Pages.TryGetValue(slug, out var page) ? page : null;

    /// <summary>Precomputed plain text of one page; empty when the snapshot carries none.</summary>
    public string SearchTextFor(string? slug)
        => slug is not null && SearchText is not null && SearchText.TryGetValue(slug, out var plain)
            ? plain
            : string.Empty;
}

/// <summary>Editing row of the settings panel.</summary>
/// <remarks>
/// Carries no HTML on purpose: an editorial page holds its pictures as base64 inside the body, so a list row with
/// the draft attached would pull every page's megabytes just to render a table of titles. The editor asks for the
/// one draft it is about to show.
/// </remarks>
/// <param name="DraftDiffers">Draft and published copy differ, so publishing would change what visitors read.</param>
public sealed record PublicPageEdit(
    string Id,
    string Slug,
    string Title,
    string? MenuTitle,
    string? IconName,
    int SortOrder,
    PublicPageStatus Status,
    bool ShowInMenu,
    bool DraftDiffers,
    DateTime? PublishedAt,
    string? PublishedByName,
    DateTime? ModifiedAt);

/// <summary>Draft input of the settings panel; publishing is a separate call.</summary>
public class PublicPageInput
{
    /// <summary>Null creates a page, otherwise the row to update.</summary>
    public string? Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? MenuTitle { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; }

    /// <summary>New draft HTML; null leaves the stored draft untouched, an empty string clears it.</summary>
    public string? DraftHtml { get; set; }

    public bool ShowInMenu { get; set; } = true;

    /// <summary>The row's <c>ModifiedAt</c> when the editor opened it; a mismatch on save is a collision.</summary>
    /// <remarks>
    /// Same expression as the panel row carries (<c>ModifiedAt ?? CreatedAt</c>), so the two are comparable after
    /// their common round trip through the database. Ignored when creating a page.
    /// </remarks>
    public DateTime? LoadedModifiedAt { get; set; }
}
