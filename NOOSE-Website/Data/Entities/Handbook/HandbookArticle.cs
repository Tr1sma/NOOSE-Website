using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Handbook;

/// <summary>One article of the handbook: how a part of the site is operated.</summary>
/// <remarks>
/// Three kinds of content, deliberately separate rather than one body:
/// <see cref="ContentHtml"/> is the prose an author may rewrite freely; <see cref="RoleplayHtml"/> is the set-apart
/// box saying what the same thing means in the roleplay, so the agency can change its own rules without touching the
/// instructions; and <see cref="DiagramKey"/> only names a drawing that lives in code, because the HTML filter allows
/// no <c>svg</c> and would strip a real one on the first save.
/// <para>
/// <see cref="NavKey"/> ties the article to a menu entry (<c>NavEntry.Key</c>). That is what lets the help button in
/// the page header resolve the current route to the right article without a second table of routes.
/// </para>
/// </remarks>
[Table("HandbuchArtikel")]
public class HandbookArticle : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("KapitelId")]
    public string ChapterId { get; set; } = string.Empty;
    public HandbookChapter? Chapter { get; set; }

    [Column("Slug")]
    public string Slug { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>One plain line for the chapter list; not HTML.</summary>
    [Column("Kurzbeschreibung")]
    public string? Summary { get; set; }

    [Column("InhaltHtml")]
    public string? ContentHtml { get; set; }

    /// <summary>The set-apart box: what this means in the roleplay. Null when there is nothing to add.</summary>
    [Column("RollenspielHtml")]
    public string? RoleplayHtml { get; set; }

    /// <summary>Names a drawing in Components/Pages/Handbook/Diagrams; an unknown key renders nothing.</summary>
    [Column("DiagrammSchluessel")]
    public string? DiagramKey { get; set; }

    /// <summary>Menu entry this article explains, so the help button can find it from a route.</summary>
    [Column("NavSchluessel")]
    public string? NavKey { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    public ICollection<HandbookStep> Steps { get; set; } = new List<HandbookStep>();

    /// <summary>Stable handle of a shipped article; null on a hand-made one, which the seeder never touches.</summary>
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
