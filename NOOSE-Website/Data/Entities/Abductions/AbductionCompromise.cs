using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Abductions;

/// <summary>A record compromised by an abduction; status is reversible back to normal.</summary>
[Table("EntfuehrungKompromittierungen")]
public class AbductionCompromise : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("EntfuehrungId")]
    public string AbductionId { get; set; } = string.Empty;
    public AgentAbduction? Abduction { get; set; }

    /// <summary>Compromised record type via nameof(T); loose link, no FK.</summary>
    [Column("ZielTyp")]
    public string TargetType { get; set; } = string.Empty;

    [Column("ZielId")]
    public string TargetId { get; set; } = string.Empty;

    [Column("Status")]
    public CompromiseStatus Status { get; set; } = CompromiseStatus.Compromised;

    /// <summary>What specifically was compromised.</summary>
    [Column("Notiz")]
    public string? Note { get; set; }

    [Column("NormalAm")]
    public DateTime? ClearedAt { get; set; }
    [Column("NormalVonId")]
    public string? ClearedById { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
