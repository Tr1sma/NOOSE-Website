using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Kasse;

/// <summary>Current state of one cash account for the overview cards.</summary>
public record KassenKontoSummary(KassenKonto Account, decimal Balance, int Count, DateTime? LastBookingAt);
