using NOOSE_Website.Data.Entities.Kasse;

namespace NOOSE_Website.Models.Kasse;

/// <summary>A ledger row with resolved booker plus the signed delta and the running balance at that point.</summary>
public record KassenBuchungDisplay(
    KassenBuchung Buchung,
    string BookedByCodename,
    decimal Delta,
    decimal BalanceAfter);
