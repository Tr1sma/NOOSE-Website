using System.Security.Claims;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Kasse;

namespace NOOSE_Website.Services;

/// <summary>Manages the two NOOSE cash accounts (Schwarzgeld/Grüngeld): a deposit/withdrawal/correction ledger with a computed balance.</summary>
public interface IKassenService
{
    /// <summary>Current balance of one account (chronological fold over live bookings).</summary>
    Task<decimal> GetBalanceAsync(KassenKonto account, CancellationToken cancellationToken = default);

    /// <summary>Current balance of one account with one booking left out (for edit-time previews/guards).</summary>
    Task<decimal> GetBalanceExcludingAsync(KassenKonto account, string excludeId, CancellationToken cancellationToken = default);

    /// <summary>State of both accounts for the overview cards.</summary>
    Task<IReadOnlyList<KassenKontoSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);

    /// <summary>Ledger of one account, newest first, each row carrying its signed delta and running balance.</summary>
    Task<IReadOnlyList<KassenBuchungDisplay>> GetLedgerAsync(KassenKonto account, CancellationToken cancellationToken = default);

    Task<KassenBuchung?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<KassenBuchungDisplay?> GetDisplayAsync(string id, CancellationToken cancellationToken = default);

    Task<KassenBuchung> BookAsync(KassenBuchungInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Books into the caller's context and open transaction, so the booking and the record that caused it commit together.</summary>
    Task<KassenBuchung> BookAsync(AppDbContext db, KassenBuchungInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, KassenBuchungInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<KassenBuchung>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
