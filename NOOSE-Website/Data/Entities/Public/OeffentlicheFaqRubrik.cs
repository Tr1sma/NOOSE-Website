using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>One main section of the public FAQ; it groups questions and carries no answer of its own.</summary>
/// <remarks>
/// The FAQ lives on the editorial page <c>/info/faq</c>, so a rubric has no address and no publish pair: the page
/// decides whether any of this is reachable at all, and <see cref="IsVisible"/> decides whether this one section is
/// part of it. Saving is therefore immediate — there is no dated statement here that a second click would protect,
/// which is what the draft/published split exists for on a press release.
/// <para>
/// Hard-deleted, like <see cref="OeffentlichesFuehrungsprofil"/>: this is editorial furniture, not a record, and a
/// rubric that still holds questions is refused rather than taken down with them.
/// </para>
/// </remarks>
[Table("OeffentlicheFaqRubriken")]
public class OeffentlicheFaqRubrik : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>One line under the heading; plain text, rendered as text and never as markup.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Icon name from the shared allowlist, never markup.</summary>
    [Column("Icon")]
    public string? IconName { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    /// <summary>Part of the public FAQ; off hides the whole section including its questions.</summary>
    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    /// <summary>The section starts expanded for every visitor.</summary>
    [Column("StandardOffen")]
    public bool DefaultOpen { get; set; }

    public ICollection<OeffentlicheFaqEintrag> Entries { get; set; } = new List<OeffentlicheFaqEintrag>();

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
