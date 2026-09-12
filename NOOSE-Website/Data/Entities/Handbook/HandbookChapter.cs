using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Handbook;

/// <summary>One chapter of the handbook; a section of the rail on /handbuch.</summary>
/// <remarks>
/// <see cref="Slug"/> is the address and the sort key of the rail, <see cref="SeedKey"/> the handle the shipped
/// content is recognised by. Two fields rather than one so a chapter can be renamed without orphaning its row.
/// </remarks>
[Table("HandbuchKapitel")]
public class HandbookChapter : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>One plain line under the heading; not HTML, so a list renders no markup.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("IconName")]
    public string? IconName { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    public ICollection<HandbookArticle> Articles { get; set; } = new List<HandbookArticle>();

    /// <summary>Stable handle of a shipped chapter; null on a hand-made one, which the seeder never touches.</summary>
    [Column("SeedSchluessel")]
    public string? SeedKey { get; set; }

    [Column("SeedRevision")]
    public int SeedRevision { get; set; }

    /// <summary>Set on the first editorial save; from then on the shipped content never overwrites this row.</summary>
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
