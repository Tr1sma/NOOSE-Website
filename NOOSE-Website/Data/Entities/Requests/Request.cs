using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Requests;

/// <summary>An inbox-workflow request with a polymorphic target; currently used for classification upgrades.</summary>
[Table("Antraege")]
public class Request : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Typ")]
    public RequestType Type { get; set; } = RequestType.Upgrade;

    /// <summary>Target record CLR type name (nameof).</summary>
    [Column("ZielTyp")]
    public string TargetType { get; set; } = string.Empty;

    [Column("ZielId")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Denormalized target display (name + case number) for the inbox.</summary>
    [Column("ZielBezeichnung")]
    public string TargetDesignation { get; set; } = string.Empty;

    [Column("ZielEinstufung")]
    public Classification TargetClassification { get; set; }

    [Column("Begruendung")]
    public string? Justification { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Requested;

    [Column("AntragstellerName")]
    public string? RequesterName { get; set; }

    [Column("EntscheiderName")]
    public string? DeciderName { get; set; }

    [Column("EntschiedenAm")]
    public DateTime? DecidedAt { get; set; }

    [Column("Entscheidungsnotiz")]
    public string? DecisionNote { get; set; }

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
