using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>Publication snapshot of an organisation, listed at <c>/organisationen</c>.</summary>
/// <remarks>
/// Every outward field lives on this row. The faction file is never read for content again, so renaming a faction or
/// editing its file changes nothing outside until someone publishes anew. <see cref="Status"/> is the only thing that
/// decides public visibility.
/// <para>
/// <see cref="FactionId"/> stays on the row for the internal side only — the timeline fan-out, the warning banner and
/// the suppression belt that hides the profile once its file becomes classified or deleted. It never leaves the house,
/// and neither does the raw threat score: outside there is a level, never a number.
/// </para>
/// </remarks>
[Table("OeffentlicheFraktionsprofile")]
public class OeffentlichesFraktionsprofil : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }

    [Column("AnzeigeName")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("KurzbeschreibungHtml")]
    public string? DescriptionHtml { get; set; }

    /// <summary>Public label of the organisation; an editorial statement, not the internal classification.</summary>
    [Column("Einordnung")]
    public PublicFactionStanding Standing { get; set; } = PublicFactionStanding.Beobachtet;

    [Column("Status")]
    public PublicProfileStatus Status { get; set; } = PublicProfileStatus.Entwurf;

    /// <summary>Hazard level as it was at publication; the raw score never leaves the house.</summary>
    [Column("OeffentlicheGefahrenstufe")]
    public HazardLevel PublicHazardLevel { get; set; } = HazardLevel.No;

    [Column("VeroeffentlichtAm")]
    public DateTime? PublishedAt { get; set; }
    [Column("VeroeffentlichtVonId")]
    public string? PublishedById { get; set; }
    public Agent? PublishedBy { get; set; }

    [Column("ZurueckgezogenAm")]
    public DateTime? RetractedAt { get; set; }
    /// <summary>Why the profile went offline; retracting without a reason is refused by the service.</summary>
    [Column("ZurueckgezogenGrund")]
    public string? RetractedReason { get; set; }

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
