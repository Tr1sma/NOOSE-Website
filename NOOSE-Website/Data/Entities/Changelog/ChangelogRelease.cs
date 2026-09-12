using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Changelog;

/// <summary>One released version of the site, the heading a set of changelog lines hangs under.</summary>
/// <remarks>
/// <see cref="Version"/> is authored, not derived. The build counter cannot play that role: BuildNumber.txt is
/// gitignored and grows on every build including local ones, so it is unknown while a line is being written and the
/// numbers of the past were never recorded at all.
/// <para>
/// <see cref="BuildNumber"/> is stamped instead by the seeder on the first start after a deploy, onto the newest
/// release that has none. A retroactive release simply carries no build number rather than an invented one.
/// </para>
/// <para>
/// No soft delete: a release is structure, not content. Hiding one is what <see cref="IsVisible"/> is for, and its
/// lines carry the recycle bin.
/// </para>
/// </remarks>
[Table("Aenderungsfassungen")]
public class ChangelogRelease : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Authored version, unique, e.g. "1.1".</summary>
    [Column("Version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>The day this version went live; the sort key readers actually see.</summary>
    [Column("Datum")]
    public DateTime Date { get; set; }

    /// <summary>Optional one-line summary of the release.</summary>
    [Column("Titel")]
    public string? Title { get; set; }

    /// <summary>Running build the release shipped as; stamped once, never overwritten.</summary>
    [Column("BuildNummer")]
    public string? BuildNumber { get; set; }

    /// <summary>Tie-breaker for two releases on the same day.</summary>
    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    public ICollection<ChangelogEntry> Entries { get; set; } = new List<ChangelogEntry>();

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
