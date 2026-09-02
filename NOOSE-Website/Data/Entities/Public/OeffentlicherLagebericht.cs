using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>A month of the agency's work in words, listed on <c>/berichte</c> once it is published.</summary>
/// <remarks>
/// Text, never figures. The internal <see cref="SituationReport"/> is a frozen statistics snapshot: it counts
/// classified records, names people with their internal file numbers and computes every distribution over the whole
/// stock. Releasing a section of it mechanically would publish exactly the numbers the public area forbids, so the
/// body here is written by leadership and <see cref="SituationReportId"/> is an anchor and a provenance note, never a
/// data source.
/// <para>
/// Two copies of what the public reads, one meaning each: Title/DraftHtml are the working copy, ContentTitle and
/// ContentHtml are what the outside world reads and are only ever written by publishing.
/// </para>
/// <para>
/// The anchor is nullable although it is required to create a row: a required navigation is INNER joined, so a report
/// whose internal counterpart was deleted afterwards would drop out of a projection silently while a count that
/// touches no navigation kept counting it.
/// </para>
/// <para>
/// No file number: the period is the address (<c>/berichte/2026-08</c>), it is quotable, never reused, and taken from
/// the anchor rather than from the caller. No unique index on it either — with soft delete that would block a month
/// forever; "one living public report per month" is a service rule over the living rows.
/// </para>
/// </remarks>
[Table("OeffentlicheLageberichte")]
public class OeffentlicherLagebericht : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The archived monthly report this text belongs to; null once that one was deleted.</summary>
    [Column("LageberichtId")]
    public string? SituationReportId { get; set; }
    public SituationReport? SituationReport { get; set; }

    /// <summary>Taken from the anchor when the row is created; immutable afterwards.</summary>
    [Column("Jahr")]
    public int Year { get; set; }

    [Column("Monat")]
    public int Month { get; set; }

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

    [Column("Status")]
    public PublicReportStatus Status { get; set; } = PublicReportStatus.Entwurf;

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
