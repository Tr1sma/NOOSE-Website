using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Abductions;

/// <summary>Records an abduction of a NOOSE agent: who took them and what information leaked.</summary>
[Table("AgentEntfuehrungen")]
public class AgentAbduction : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    /// <summary>The abducted agent.</summary>
    [Column("OpferAgentId")]
    public string VictimAgentId { get; set; } = string.Empty;
    public Agent? VictimAgent { get; set; }

    /// <summary>Perpetrator record type: nameof(Faction)/nameof(PersonGroup)/nameof(Person). Loose link, no FK.</summary>
    [Column("TaeterTyp")]
    public string PerpetratorType { get; set; } = string.Empty;

    [Column("TaeterId")]
    public string PerpetratorId { get; set; } = string.Empty;

    /// <summary>When the abduction happened (RP time, stored UTC).</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    /// <summary>When captivity ended; null while still held.</summary>
    [Column("FreigelassenAm")]
    public DateTime? ReleasedAt { get; set; }

    [Column("Ort")]
    public string? Location { get; set; }

    [Column("Wahrheitsserum")]
    public bool TruthSerum { get; set; }

    /// <summary>Whether any information leaked at all.</summary>
    [Column("Informationsabfluss")]
    public bool InformationLeaked { get; set; }

    [Column("LeakKategorien")]
    public LeakCategory LeakCategories { get; set; }

    [Column("Schweregrad")]
    public LeakSeverity LeakSeverity { get; set; }

    [Column("Notizen")]
    public string? Notes { get; set; }

    /// <summary>Records rendered compromised by this abduction; reversible.</summary>
    public List<AbductionCompromise> Compromises { get; set; } = new();

    [Column("Ausgang")]
    public AbductionOutcome Outcome { get; set; }

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
