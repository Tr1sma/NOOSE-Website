using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>A global tag/label assignable to any case. No soft-delete: it's a lookup value, hard-deleted (FK cascade clears assignments) so the unique index on Name stays clean.</summary>
[Table("Tags")]
public class Tag : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional MudBlazor colour name (e.g. "Primary"/"Info") for the chip.</summary>
    [Column("Farbe")]
    public string? Colour { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
