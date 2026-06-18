using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Admin-defined custom field for a record type; per-record values live in CustomFieldValue.</summary>
[Table("CustomFeldDefinitionen")]
public class CustomFieldDefinition : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Record type the field applies to, e.g. nameof(Person) (CLR type name).</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Column("FeldTyp")]
    public CustomFieldType FieldType { get; set; }

    /// <summary>Select options (one per line) for dropdown field types.</summary>
    [Column("Optionen")]
    public string? Options { get; set; }

    /// <summary>Required: a value must be set when saving.</summary>
    [Column("Pflicht")]
    public bool Mandatory { get; set; }

    /// <summary>Display order in the panel (smaller first).</summary>
    [Column("Reihenfolge")]
    public int Order { get; set; }

    /// <summary>Only active fields appear in the record's custom-fields panel.</summary>
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

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
