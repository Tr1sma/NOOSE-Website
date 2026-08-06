using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Kasse;

namespace NOOSE_Website.Services;

/// <summary>NOOSE treasury: two cash accounts with a deposit/withdrawal/correction ledger; the balance is folded from live bookings, never stored.</summary>
public class KassenService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseNumberService caseNumber) : IKassenService
{
    private const string CasePrefix = "KAS";

    // ---- reads ----

    public async Task<decimal> GetBalanceAsync(KassenKonto account, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ComputeBalanceAsync(db, account, null, cancellationToken);
    }

    public async Task<IReadOnlyList<KassenKontoSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var summaries = new List<KassenKontoSummary>(KassenKontoDisplay.All.Count);
        foreach (var account in KassenKontoDisplay.All)
        {
            var rows = await OrderedAsync(db, account, cancellationToken);
            var balance = Fold(rows.Select(r => (r.Kind, r.Amount)));
            var last = rows.Count == 0 ? (DateTime?)null : rows[^1].Timestamp;
            summaries.Add(new KassenKontoSummary(account, balance, rows.Count, last));
        }
        return summaries;
    }

    public async Task<IReadOnlyList<KassenBuchungDisplay>> GetLedgerAsync(KassenKonto account, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.KassenBuchungen
            .Include(b => b.BookedBy)
            .Where(b => b.Account == account)
            .OrderBy(b => b.Timestamp).ThenBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

        var list = new List<KassenBuchungDisplay>(rows.Count);
        decimal running = 0m;
        foreach (var b in rows)
        {
            var before = running;
            running = Apply(running, b.Kind, b.Amount);
            var codename = b.BookedBy?.Codename ?? "(unbekannt)";
            list.Add(new KassenBuchungDisplay(b, codename, running - before, running));
        }
        list.Reverse(); // newest first for display
        return list;
    }

    public async Task<KassenBuchung?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.KassenBuchungen.Include(b => b.BookedBy).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<KassenBuchungDisplay?> GetDisplayAsync(string id, CancellationToken cancellationToken = default)
    {
        var booking = await GetAsync(id, cancellationToken);
        if (booking is null)
        {
            return null;
        }
        var ledger = await GetLedgerAsync(booking.Account, cancellationToken);
        return ledger.FirstOrDefault(d => d.Buchung.Id == id);
    }

    // ---- writes ----

    public async Task<KassenBuchung> BookAsync(KassenBuchungInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        Validate(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNonNegativeAsync(db, input, null, cancellationToken);
        // case-number allocation needs the caller's transaction so counter + record commit together
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var booking = new KassenBuchung
        {
            CaseNumber = await caseNumber.NextAsync(db, CasePrefix, cancellationToken),
            Account = input.Account,
            Kind = input.Kind,
            Amount = input.Amount,
            Reason = input.Reason.TrimToNull(),
            BookedById = string.IsNullOrWhiteSpace(input.BookedById) ? actor.GetAgentId() : input.BookedById,
            Timestamp = input.Timestamp,
        };
        db.KassenBuchungen.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return booking;
    }

    public async Task UpdateAsync(string id, KassenBuchungInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        Validate(input);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var booking = await db.KassenBuchungen.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Kassenbuchung '{id}' nicht gefunden.");

        // resulting balance is computed without this booking's own (old) row
        await EnsureNonNegativeAsync(db, input, id, cancellationToken);

        booking.Account = input.Account;
        booking.Kind = input.Kind;
        booking.Amount = input.Amount;
        booking.Reason = input.Reason.TrimToNull();
        if (!string.IsNullOrWhiteSpace(input.BookedById))
        {
            booking.BookedById = input.BookedById;
        }
        booking.Timestamp = input.Timestamp;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var booking = await db.KassenBuchungen.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (booking is null)
        {
            return;
        }
        // Interceptor rewrites Remove to soft-delete; downstream balances recompute on read.
        db.KassenBuchungen.Remove(booking);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<KassenBuchung>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.KassenBuchungen.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(b => b.IsDeleted)
            .OrderByDescending(b => b.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        RequireManage(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var booking = await db.KassenBuchungen.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Kassenbuchung '{id}' nicht gefunden.");
        booking.IsDeleted = false;
        booking.DeletedAt = null;
        booking.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- balance engine ----

    /// <summary>Chronological fold of live bookings: deposit adds, withdrawal subtracts, correction sets the absolute balance.</summary>
    private static decimal Apply(decimal running, KassenBuchungArt kind, decimal amount) => kind switch
    {
        KassenBuchungArt.Einzahlung => running + amount,
        KassenBuchungArt.Auszahlung => running - amount,
        KassenBuchungArt.Korrektur => amount,
        _ => running,
    };

    private static decimal Fold(IEnumerable<(KassenBuchungArt Kind, decimal Amount)> ordered)
    {
        decimal running = 0m;
        foreach (var (kind, amount) in ordered)
        {
            running = Apply(running, kind, amount);
        }
        return running;
    }

    private static async Task<List<KassenBuchung>> OrderedAsync(AppDbContext db, KassenKonto account, CancellationToken cancellationToken)
        => await db.KassenBuchungen.AsNoTracking()
            .Where(b => b.Account == account)
            .OrderBy(b => b.Timestamp).ThenBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Current balance of an account; excludeId leaves one booking out (for edit-time checks).</summary>
    private static async Task<decimal> ComputeBalanceAsync(AppDbContext db, KassenKonto account, string? excludeId, CancellationToken cancellationToken)
    {
        var rows = await db.KassenBuchungen.AsNoTracking()
            .Where(b => b.Account == account && (excludeId == null || b.Id != excludeId))
            .OrderBy(b => b.Timestamp).ThenBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .Select(b => new { b.Kind, b.Amount })
            .ToListAsync(cancellationToken);
        return Fold(rows.Select(r => (r.Kind, r.Amount)));
    }

    /// <summary>Blocks a withdrawal/correction that would drive the account below zero; deposits never reduce the balance.</summary>
    private static async Task EnsureNonNegativeAsync(AppDbContext db, KassenBuchungInput input, string? excludeId, CancellationToken cancellationToken)
    {
        if (input.Kind == KassenBuchungArt.Einzahlung)
        {
            return;
        }
        var current = await ComputeBalanceAsync(db, input.Account, excludeId, cancellationToken);
        var resulting = Apply(current, input.Kind, input.Amount);
        if (resulting < 0)
        {
            throw new InvalidOperationException(
                $"Die Buchung würde die Kasse „{KassenKontoDisplay.Name(input.Account)}“ ins Minus bringen " +
                $"(aktuell {Money.Format(current)}, Ergebnis {Money.Format(resulting)}).");
        }
    }

    private static void Validate(KassenBuchungInput input)
    {
        if (input.Amount < 0)
        {
            throw new InvalidOperationException("Der Betrag darf nicht negativ sein.");
        }
        if (input.Kind != KassenBuchungArt.Korrektur && input.Amount <= 0)
        {
            throw new InvalidOperationException("Bitte einen Betrag größer 0 angeben.");
        }
    }

    private static void RequireManage(ClaimsPrincipal actor)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);
    }
}
