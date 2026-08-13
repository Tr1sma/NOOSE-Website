using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>Publication snapshot of a wanted notice, reachable at <c>/gesucht/{CaseNumber}</c>.</summary>
/// <remarks>
/// Every outward field lives on this row. The record it was drawn from is never read for content again, so renaming a
/// person or editing the file changes nothing outside until someone publishes again. <see cref="Status"/> is the only
/// thing that decides public visibility.
/// <para>
/// <see cref="PersonId"/> stays on the row for the internal side only — the timeline fan-out, the warning banner and
/// the suppression belt that hides the notice once its file becomes classified or deleted. It never leaves the house.
/// </para>
/// </remarks>
[Table("OeffentlicheFahndungen")]
public class OeffentlicheFahndung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Public case number, prefix <c>FA</c>; minted on the first publication only.</summary>
    /// <remarks>Null while the notice is a draft: a draft that never ships must not burn a public number.</remarks>
    [Column("Aktenzeichen")]
    public string? CaseNumber { get; set; }

    [Column("Art")]
    public PublicWantedKind Kind { get; set; } = PublicWantedKind.Fahndung;

    [Column("Status")]
    public PublicWantedStatus Status { get; set; } = PublicWantedStatus.Entwurf;

    [Column("PersonId")]
    public string? PersonId { get; set; }
    public Person? Person { get; set; }

    /// <summary>Faction behind the notice; carried from the start so the public faction profile needs no migration.</summary>
    [Column("FraktionId")]
    public string? FactionId { get; set; }
    public Faction? Faction { get; set; }

    [Column("AnzeigeName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Aliases chosen by the author, not pulled from the file: an alias may be informant-sourced.</summary>
    [Column("AliaseText")]
    public string? AliasText { get; set; }

    /// <summary>Name of the copy under the public upload path, never a <c>PersonFoto</c> file name.</summary>
    [Column("FotoDateiname")]
    public string? PhotoFileName { get; set; }

    /// <summary>Content type of the copy; the delivery endpoint needs it and the extension is not a second truth.</summary>
    [Column("FotoTyp")]
    public string? PhotoContentType { get; set; }

    /// <summary>Which file photo the copy came from; shown in the editor, never read by the delivery path.</summary>
    [Column("FotoQuellId")]
    public string? PhotoSourceId { get; set; }

    [Column("VorwurfHtml")]
    public string? ChargeHtml { get; set; }

    [Column("LetzteGegend")]
    public string? LastArea { get; set; }

    [Column("FahrzeugText")]
    public string? VehicleText { get; set; }

    /// <summary>Hazard level as it was at publication; the raw score never leaves the house.</summary>
    [Column("OeffentlicheGefahrenstufe")]
    public HazardLevel PublicHazardLevel { get; set; } = HazardLevel.No;

    /// <summary>Filtered out on the read side; the sweep that flips the status arrives with the archive phase.</summary>
    [Column("AblaufDatum")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Column now, meaning with the bounty phase.</summary>
    [Column("KopfgeldIstObergrenze")]
    public bool BountyIsCap { get; set; }

    [Column("VeroeffentlichtAm")]
    public DateTime? PublishedAt { get; set; }
    [Column("VeroeffentlichtVonId")]
    public string? PublishedById { get; set; }
    public Agent? PublishedBy { get; set; }

    [Column("ZurueckgezogenAm")]
    public DateTime? RetractedAt { get; set; }
    /// <summary>Why the notice went offline; retracting without a reason is refused by the service.</summary>
    [Column("ZurueckgezogenGrund")]
    public string? RetractedReason { get; set; }

    [Column("GefasstAm")]
    public DateTime? CapturedAt { get; set; }

    /// <summary>Declared, never written here: an audited increment per anonymous view writes one log row per request.</summary>
    [Column("AufrufZaehler")]
    public int ViewCount { get; set; }

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
