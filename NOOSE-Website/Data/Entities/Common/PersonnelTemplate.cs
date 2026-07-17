using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>A personnel-record template: a predefined HTML body copied into the editor when creating a commendation, disciplinary note, or promotion; leadership-managed.</summary>
[Table("PersonalVorlagen")]
public class PersonnelTemplate : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Which personnel record type this template applies to.</summary>
    [Column("Art")]
    public PersonnelTemplateKind Kind { get; set; }

    /// <summary>Template name; unique per kind (service-checked).</summary>
    public string Name { get; set; } = string.Empty;

    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Sanitized HTML body.</summary>
    [Column("InhaltHtml")]
    public string ContentHtml { get; set; } = string.Empty;

    /// <summary>Only active templates appear in the picker.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    [Column("Sortierung")]
    public int Sorting { get; set; }

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
