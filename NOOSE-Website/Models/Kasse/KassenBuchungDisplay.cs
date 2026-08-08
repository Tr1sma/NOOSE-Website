using NOOSE_Website.Data.Entities.Kasse;

namespace NOOSE_Website.Models.Kasse;

/// <summary>A ledger row with resolved booker plus the signed delta and the running balance at that point.</summary>
/// <remarks>The financing fields are set only for a payout a funding request booked; the booking itself holds no back-reference.</remarks>
public record KassenBuchungDisplay(
    KassenBuchung Buchung,
    string BookedByCodename,
    decimal Delta,
    decimal BalanceAfter,
    string? FinancingRequestId = null,
    string? FinancingCaseNumber = null);
