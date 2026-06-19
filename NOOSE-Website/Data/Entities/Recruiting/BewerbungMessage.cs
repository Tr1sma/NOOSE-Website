using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>A recruiting message; either the HRB-internal thread or the applicant-facing conversation (by audience).</summary>
[Table("BewerbungNachrichten")]
public class BewerbungMessage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BewerbungId { get; set; } = string.Empty;
    public Bewerbung? Bewerbung { get; set; }

    [Column("Zielgruppe")]
    public BewerbungMessageAudience Audience { get; set; }

    /// <summary>Raw text incl. inline @{Type:Id} mention tokens (internal thread only).</summary>
    public string Text { get; set; } = string.Empty;

    [Column("AutorName")]
    public string? AuthorName { get; set; }

    /// <summary>True when posted by the applicant (only valid for the Bewerber audience).</summary>
    [Column("VonBewerber")]
    public bool AuthorIsApplicant { get; set; }

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
