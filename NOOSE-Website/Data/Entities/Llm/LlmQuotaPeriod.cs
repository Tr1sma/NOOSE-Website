using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Llm;

/// <summary>A closed weekly AI-token period of one agent; append-only, so once written a week's carry-over never moves again.</summary>
/// <remarks>
/// Closing happens lazily on the first quota read after the week ended, with the rank, override and rules in
/// force at THAT moment — the same accepted limitation the funding budget carries. The carry-over is additionally
/// capped on read, so a rule lowered after the close can never hand out the old amount.
/// </remarks>
[Table("KiKontingentperioden")]
public class LlmQuotaPeriod
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>ISO-8601 year; around New Year this differs from the calendar year.</summary>
    [Column("Jahr")]
    public int Year { get; set; }

    [Column("Woche")]
    public int Week { get; set; }

    [Column("Grundkontingent")]
    public long BaseWeekly { get; set; }

    [Column("UebertragEin")]
    public long CarryIn { get; set; }

    [Column("Verbraucht")]
    public long Consumed { get; set; }

    /// <summary>What this week hands to the next one; only the direct successor may use it.</summary>
    [Column("UebertragAus")]
    public long CarryOut { get; set; }

    [Column("Uebertragsprozent")]
    public int CarryPercent { get; set; }

    [Column("DienstgradBeiAbschluss")]
    public Rank? RankAtClose { get; set; }

    [Column("AbgeschlossenAm")]
    public DateTime ClosedAt { get; set; }
}
