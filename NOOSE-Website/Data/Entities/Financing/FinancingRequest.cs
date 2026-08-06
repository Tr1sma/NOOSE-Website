using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Financing;

/// <summary>An agent's funding request: a basket of catalog positions decided as one and paid out as one treasury withdrawal.</summary>
[Table("Finanzierungsantraege")]
public class FinancingRequest : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    /// <summary>Requesting agent; also the payout recipient.</summary>
    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    public FinancingStatus Status { get; set; } = FinancingStatus.Requested;

    [Column("Begruendung")]
    public string Justification { get; set; } = string.Empty;

    /// <summary>Sum of the requested lines at full price.</summary>
    [Column("BeantragteSumme")]
    public decimal RequestedGross { get; set; }

    /// <summary>Sum of the requested lines' subsidies.</summary>
    [Column("BeantragterZuschuss")]
    public decimal RequestedSubsidy { get; set; }

    /// <summary>Subsidy after the decision-time quantity cuts; this is what gets paid out.</summary>
    [Column("GenehmigterZuschuss")]
    public decimal? ApprovedSubsidy { get; set; }

    /// <summary>Budget period the reservation belongs to, stamped on approval so a later payout cannot shift it.</summary>
    [Column("BudgetJahr")]
    public int? BudgetYear { get; set; }
    [Column("BudgetMonat")]
    public int? BudgetMonth { get; set; }

    [Column("EntscheiderName")]
    public string? DeciderName { get; set; }
    [Column("EntschiedenAm")]
    public DateTime? DecidedAt { get; set; }
    [Column("Entscheidungsnotiz")]
    public string? DecisionNote { get; set; }

    /// <summary>How far the approval went past the remaining budget.</summary>
    [Column("Ueberschreitungsbetrag")]
    public decimal? OverrunAmount { get; set; }
    [Column("UeberschreitungsBegruendung")]
    public string? OverrunReason { get; set; }

    [Column("AusgezahltAm")]
    public DateTime? PaidAt { get; set; }
    [Column("AusgezahltVonName")]
    public string? PaidByName { get; set; }

    /// <summary>Treasury booking created by the payout; unique, so the same request cannot be paid twice.</summary>
    public string? KassenBuchungId { get; set; }

    public List<FinancingRequestLine> Lines { get; set; } = new();

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
