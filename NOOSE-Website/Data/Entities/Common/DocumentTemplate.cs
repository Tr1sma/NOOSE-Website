using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>A document template: a predefined HTML body (may contain placeholder tokens) copied into the editor on new-document creation.</summary>
[Table("DokumentVorlagen")]
public class DocumentTemplate : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Template name; unique (service-checked).</summary>
    public string Name { get; set; } = string.Empty;

    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Kategorie")]
    public string? Category { get; set; }

    /// <summary>Sanitized HTML body (may contain placeholder tokens).</summary>
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
