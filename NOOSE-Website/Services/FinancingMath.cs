using NOOSE_Website.Data.Entities.Financing;

namespace NOOSE_Website.Services;

/// <summary>Money arithmetic of a funding request. Single source of truth for service, UI and tests — never recompute inline.</summary>
public static class FinancingMath
{
    /// <summary>Full price of a quantity.</summary>
    public static decimal Gross(decimal unitPrice, int quantity) => unitPrice * quantity;

    /// <summary>What NOOSE covers, rounded to whole dollars per line, so request, budget and treasury booking always show the same number.</summary>
    public static decimal Subsidy(decimal unitPrice, int quantity, int subsidyPercent)
        => Math.Round(unitPrice * quantity * subsidyPercent / 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>What the agent pays out of pocket.</summary>
    public static decimal OwnShare(decimal unitPrice, int quantity, int subsidyPercent)
        => Gross(unitPrice, quantity) - Subsidy(unitPrice, quantity, subsidyPercent);

    /// <summary>Quantity that currently counts: the approved one once decided, otherwise the requested one.</summary>
    public static int EffectiveQuantity(FinancingRequestLine line) => line.ApprovedQuantity ?? line.Quantity;

    public static decimal RequestedGross(FinancingRequestLine line) => Gross(line.UnitPrice, line.Quantity);

    public static decimal RequestedSubsidy(FinancingRequestLine line)
        => Subsidy(line.UnitPrice, line.Quantity, line.SubsidyPercent);

    public static decimal EffectiveGross(FinancingRequestLine line) => Gross(line.UnitPrice, EffectiveQuantity(line));

    public static decimal EffectiveSubsidy(FinancingRequestLine line)
        => Subsidy(line.UnitPrice, EffectiveQuantity(line), line.SubsidyPercent);

    /// <summary>Sum of the lines' subsidies at the given quantities; the sum of rounded lines, not a rounded sum.</summary>
    public static decimal SubsidyTotal(IEnumerable<FinancingRequestLine> lines, Func<FinancingRequestLine, int> quantity)
        => lines.Sum(l => Subsidy(l.UnitPrice, quantity(l), l.SubsidyPercent));

    /// <summary>Carry-over a month hands to the next: the unused rest times the rank's share, rounded to whole dollars.</summary>
    public static decimal CarryOut(decimal remaining, int carryPercent)
        => remaining <= 0 || carryPercent <= 0
            ? 0m
            : Math.Round(remaining * carryPercent / 100m, 0, MidpointRounding.AwayFromZero);
}
