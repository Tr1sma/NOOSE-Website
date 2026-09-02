using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>A standing public warning of the agency, listed on <c>/warnungen</c> while it is published and valid.</summary>
/// <remarks>
/// Not a <c>Warnhinweis</c>: that one is a chip label attached to a wanted notice. This is an announcement of its own,
/// with a body and an expiry, and the two tables never meet.
/// <para>
/// Two copies of what the public reads, one meaning each: Title/DraftHtml are the working copy an author may change at
/// any time, ContentTitle/ContentHtml are what the outside world reads and are only ever written by publishing. Same
/// reason as a press release — a saved typo fix must not silently rewrite a statement that already went out.
/// </para>
/// <para>
/// <see cref="ValidUntil"/> is the exception and has no second copy: extending a warning is not a new statement, and
/// demanding a re-publication would let a warning expire because nobody pressed the second button. It is read live and
/// therefore takes effect at once.
/// </para>
/// <para>
/// There is no public file number and no page of its own: a warning is short, so the hub carries the whole text.
/// </para>
/// </remarks>
[Table("OeffentlicheWarnungen")]
public class OeffentlicheWarnung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Working copy of the headline; never leaves the house.</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>The working copy of the body; never leaves the house.</summary>
    [Column("EntwurfHtml")]
    public string? DraftHtml { get; set; }

    /// <summary>Headline the public reads; written by publishing only.</summary>
    [Column("InhaltTitel")]
    public string? ContentTitle { get; set; }

    /// <summary>Body the public reads; written by publishing only.</summary>
    [Column("InhaltHtml")]
    public string? ContentHtml { get; set; }

    /// <summary>Expiry; null means the warning stands until it is retracted.</summary>
    [Column("GueltigBis")]
    public DateTime? ValidUntil { get; set; }

    [Column("Status")]
    public PublicWarningStatus Status { get; set; } = PublicWarningStatus.Entwurf;

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
