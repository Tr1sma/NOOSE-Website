using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>Switch row for one public-area module; the catalog defines which keys exist.</summary>
/// <remarks>
/// One row per key in <c>PublicModules.All</c>, seeded on start. No soft delete on purpose: the catalog owns the
/// set of keys, a row only carries the operator's choices for one of them, so deleting a row would mean the module
/// silently falls back to its default instead of staying off.
/// </remarks>
[Table("OeffentlicheModule")]
public class OeffentlichesModul : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Catalog key; a row whose key left the catalog is ignored on read.</summary>
    [Column("Schluessel")]
    public string Key { get; set; } = string.Empty;

    [Column("IstAktiv")]
    public bool IsEnabled { get; set; }

    /// <summary>Shown on the route while the module is off; empty falls back to a generic notice.</summary>
    [Column("OfflineText")]
    public string? OfflineText { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    /// <summary>Overrides the catalog label in the public nav.</summary>
    [Column("LabelUeberschreibung")]
    public string? LabelOverride { get; set; }

    /// <summary>Overrides the catalog icon by name from <c>PublicModules.IconChoices</c>, never raw markup.</summary>
    /// <remarks>
    /// A name, not the icon itself: MudBlazor renders an icon value as markup, so a free-text SVG saved here would
    /// run for every anonymous visitor. The allowlist makes that impossible instead of merely unlikely.
    /// </remarks>
    [Column("IconUeberschreibung")]
    public string? IconOverride { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
