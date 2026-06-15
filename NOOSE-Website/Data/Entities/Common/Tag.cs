using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Ein global verwaltetes Tag/Label (Stammdaten), das beliebigen Akten über eine
/// <see cref="TagZuordnung"/> zugeordnet werden kann. Bewusst <b>ohne</b> Soft-Delete: ein Tag ist
/// keine Akte, sondern ein Lookup-Wert – beim Löschen wird es hart entfernt (FK-Cascade räumt die
/// Zuordnungen ab), wodurch der eindeutige Index auf <see cref="Name"/> sauber bleibt.
/// </summary>
[Table("Tags")]
public class Tag : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Farbe (MudBlazor-Color-Name wie „Primary"/„Info") für die Chip-Darstellung.</summary>
    [Column("Farbe")]
    public string? Colour { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
