using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>
/// One editorial warning chip ("bewaffnet", "nicht selbst eingreifen") a wanted notice can carry outside.
/// </summary>
/// <remarks>
/// No soft-delete, same reasoning as <see cref="Common.Tag"/>: it is a lookup value, hard-deleted so the unique index
/// on Bezeichnung stays clean and the FK cascade clears the assignments. The normal way to take one out of use is
/// IstAktiv, which also removes it from every live notice — that is deliberate, because a warning that no longer
/// applies must be retractable without editing forty notices by hand.
/// </remarks>
[Table("Warnhinweise")]
public class Warnhinweis : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Bezeichnung")]
    public string Name { get; set; } = string.Empty;

    /// <summary>MudBlazor colour NAME from the allowlist; anything else would reach an anonymous page unchecked.</summary>
    [Column("Farbe")]
    public string? Colour { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
