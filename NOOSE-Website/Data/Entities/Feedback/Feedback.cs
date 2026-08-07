using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Feedback;

/// <summary>Agent feedback about the website itself.</summary>
[Table("FeedbackMeldungen")]
public sealed class Feedback : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>What kind of feedback this is.</summary>
    [Column("Meldungsart")]
    public FeedbackKind Kind { get; set; } = FeedbackKind.Improvement;

    /// <summary>Route the feedback was filed from.</summary>
    [Column("Seite")]
    public string? PageRoute { get; set; }

    /// <summary>Active tab on the page, if any.</summary>
    [Column("SeitenTab")]
    public string? PageTab { get; set; }

    /// <summary>Free text of the report.</summary>
    [Column("Text")]
    public string Text { get; set; } = string.Empty;

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
