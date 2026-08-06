using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Financing;

/// <summary>A closed monthly budget period of one agent; append-only, so once written a month's carry-over never moves again.</summary>
/// <remarks>
/// Accepted limitation: closing happens lazily on the first budget read or approval after the month ended,
/// with the rank, override and rules in force at THAT moment. A promotion or rule change inside that window
/// is therefore applied to the month it did not govern, and then frozen.
/// </remarks>
[Table("Finanzierungsbudgetperioden")]
public class FinancingBudgetPeriod
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    [Column("Jahr")]
    public int Year { get; set; }

    [Column("Monat")]
    public int Month { get; set; }

    [Column("Grundbudget")]
    public decimal BaseBudget { get; set; }

    [Column("UebertragEin")]
    public decimal CarryIn { get; set; }

    [Column("Verbraucht")]
    public decimal Consumed { get; set; }

    /// <summary>What this month hands to the next one; only the direct successor may use it.</summary>
    [Column("UebertragAus")]
    public decimal CarryOut { get; set; }

    [Column("Uebertragsprozent")]
    public int CarryPercent { get; set; }

    [Column("DienstgradBeiAbschluss")]
    public Rank? RankAtClose { get; set; }

    [Column("AbgeschlossenAm")]
    public DateTime ClosedAt { get; set; }
}
