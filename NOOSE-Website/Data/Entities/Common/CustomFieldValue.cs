using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>A concrete custom-field value on a record (polymorphic by entity type + id); always stored as a string.</summary>
[Table("CustomFeldWerte")]
public class CustomFieldValue : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Id of the owning field definition (loose link, no FK).</summary>
    [Column("CustomFeldDefinitionId")]
    public string CustomFieldDefinitionId { get; set; } = string.Empty;

    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Value as string (number invariant, date ISO, yes/no as true/false).</summary>
    [Column("Wert")]
    public string? Value { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
