using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Changelog;

/// <summary>One line of the changelog, written for the reader of the site, not for its author.</summary>
/// <remarks>
/// The title is the whole entry on purpose: no body, no technology, no file names. A reader wants to know what
/// changed for them, and a field that allows more invites the commit message back in.
/// <para>
/// Rows arrive two ways. A seeded row carries a <see cref="SeedKey"/> and belongs to the list shipped with the code;
/// a hand-written row has none and the seeder never touches it. On a seeded row <see cref="IsCustomised"/> is the
/// promise that an editorial change is never overwritten: once it is set, the seeder leaves the row alone even when
/// it ships a newer <see cref="SeedRevision"/>, and only an explicit "take the newer version" resets it.
/// </para>
/// </remarks>
[Table("Aenderungseintraege")]
public class ChangelogEntry : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FassungId")]
    public string ReleaseId { get; set; } = string.Empty;
    public ChangelogRelease? Release { get; set; }

    [Column("Art")]
    public ChangelogKind Kind { get; set; } = ChangelogKind.Neu;

    /// <summary>The entry itself: one short sentence in plain language.</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Area of the site it touched, e.g. "Fahndung"; free text, used as a filter.</summary>
    [Column("Bereich")]
    public string? Area { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    /// <summary>Stable handle of a shipped entry; null on a hand-written one, which the seeder then never touches.</summary>
    [Column("SeedSchluessel")]
    public string? SeedKey { get; set; }

    /// <summary>Revision of the shipped text this row was last written from.</summary>
    [Column("SeedRevision")]
    public int SeedRevision { get; set; }

    /// <summary>Set on the first editorial save; from then on the shipped list never overwrites this row.</summary>
    [Column("IstAngepasst")]
    public bool IsCustomised { get; set; }

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
