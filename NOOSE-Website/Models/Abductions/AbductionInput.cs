using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Abductions;

/// <summary>Form model for creating/editing an agent abduction.</summary>
public class AbductionInput
{
    public string VictimAgentId { get; set; } = string.Empty;

    /// <summary>Perpetrator record type: nameof(Faction)/nameof(PersonGroup)/nameof(Person).</summary>
    public string PerpetratorType { get; set; } = string.Empty;
    public string PerpetratorId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; set; }
    public string? Location { get; set; }
    public bool TruthSerum { get; set; }
    public bool InformationLeaked { get; set; }
    public LeakCategory LeakCategories { get; set; }
    public LeakSeverity LeakSeverity { get; set; } = LeakSeverity.None;
    public string? Notes { get; set; }
    public AbductionOutcome Outcome { get; set; } = AbductionOutcome.StillHeld;

    /// <summary>Records compromised by the leak; persisted as AbductionCompromise rows on save.</summary>
    public List<CompromiseTargetInput> Compromises { get; set; } = new();
}

/// <summary>One record picked as compromised in the editor; Display/Cleared are UI-only.</summary>
public class CompromiseTargetInput
{
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    /// <summary>UI hint: this existing entry was already re-classified as normal.</summary>
    public bool Cleared { get; set; }
}
