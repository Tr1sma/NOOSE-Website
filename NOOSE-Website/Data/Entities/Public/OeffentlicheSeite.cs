using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>Editorial page of the public area, reachable at <c>/info/{Slug}</c>.</summary>
/// <remarks>
/// Two HTML columns, one meaning each: <see cref="DraftHtml"/> is the working copy an author may change at any time,
/// <see cref="ContentHtml"/> is the copy the outside world reads and is only ever written by publishing. Without that
/// split every save would be a publication, which is exactly what the phase is meant to prevent.
/// <para>
/// <see cref="ShowInMenu"/> is not a second publish switch: it decides whether the page appears in the hub and the
/// menu, so a published page can stay reachable by direct link only (a form's terms page, for instance).
/// </para>
/// </remarks>
[Table("OeffentlicheSeiten")]
public class OeffentlicheSeite : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>URL segment; lowercase, validated on write because it lands in a route.</summary>
    [Column("Slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Short title for the hub and menu; falls back to <see cref="Title"/> when empty.</summary>
    [Column("MenueTitel")]
    public string? MenuTitle { get; set; }

    /// <summary>Icon name from the shared allowlist, never markup.</summary>
    [Column("Icon")]
    public string? IconName { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    /// <summary>What the public sees; written by publishing only.</summary>
    [Column("InhaltHtml")]
    public string? ContentHtml { get; set; }

    /// <summary>The working copy; never leaves the house.</summary>
    [Column("EntwurfHtml")]
    public string? DraftHtml { get; set; }

    [Column("Status")]
    public PublicPageStatus Status { get; set; } = PublicPageStatus.Entwurf;

    /// <summary>Listed in hub and menu; a published page may deliberately stay unlisted.</summary>
    [Column("ImMenue")]
    public bool ShowInMenu { get; set; } = true;

    [Column("VeroeffentlichtAm")]
    public DateTime? PublishedAt { get; set; }
    [Column("VeroeffentlichtVonId")]
    public string? PublishedById { get; set; }
    public Agent? PublishedBy { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
