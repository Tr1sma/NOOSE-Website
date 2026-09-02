using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicKpiService" />
public sealed class PublicKpiService(IDbContextFactory<AppDbContext> dbFactory) : IPublicKpiService
{
    /// <summary>Windows the panel offers; anything outside is clamped rather than refused.</summary>
    private const int MinDays = 1;
    private const int MaxDays = 365;

    /// <summary>Notices in the attention ranking.</summary>
    private const int TopNotices = 5;

    public async Task<PublicKpiReport> GetAsync(int days, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        var window = Math.Clamp(days, MinDays, MaxDays);
        // UtcNow, never Now: every stored stamp is UTC and a MySQL datetime carries no offset
        var since = DateTime.UtcNow.AddDays(-window);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var tips = await TipsAsync(db, since, cancellationToken);
        var rewards = await RewardsAsync(db, since, cancellationToken);
        var tickets = await TicketsAsync(db, since, cancellationToken);
        var views = await ViewsAsync(db, since, actor, cancellationToken);

        return new PublicKpiReport(window, tips, rewards, tickets, views);
    }

    /// <summary>Four counts over the same window, each naming a predicate that already exists.</summary>
    private static async Task<PublicKpiTips> TipsAsync(AppDbContext db, DateTime since, CancellationToken ct)
    {
        var rows = db.Hinweise.AsNoTracking().Where(h => h.CreatedAt >= since);
        return new PublicKpiTips(
            await rows.CountAsync(ct),
            await rows.CountAsync(TipRules.ConfirmedRows, ct),
            await rows.CountAsync(TipRules.CaptureRows, ct),
            await rows.CountAsync(TipRules.ClosedRows, ct),
            await rows.CountAsync(TipRules.OpenRows, ct));
    }

    /// <summary>What was paid, and against how many notices — the two halves of a cost per arrest.</summary>
    /// <remarks>
    /// The two persisted columns record what actually happened; the allocation rule is not recomputed here, because
    /// it decides a future payout and says nothing about one that already went out.
    /// </remarks>
    private static async Task<PublicKpiRewards> RewardsAsync(AppDbContext db, DateTime since, CancellationToken ct)
    {
        var rows = await db.HinweisBelohnungen.AsNoTracking()
            .Where(r => r.PaidAt >= since)
            .Select(r => new
            {
                r.Amount,
                Booked = r.KassenBuchungId != null,
                HandedOver = r.SelfPaidAt != null,
                NoticeId = r.Share!.WantedId,
            })
            .ToListAsync(ct);

        var captured = await db.OeffentlicheFahndungen.AsNoTracking()
            .Where(f => f.Status == PublicWantedStatus.Gefasst && f.CapturedAt >= since)
            .Select(f => f.Id)
            .ToListAsync(ct);

        // The share is measured over the ARRESTS, so its numerator asks whether each of them was ever rewarded: a
        // payout made in this window can belong to an arrest from before it, and dividing one cohort by the other
        // produced shares above 100 %. Intersected in memory rather than through a WHERE IN over the capture ids —
        // a year-long window would put thousands of parameters into one statement.
        var rewarded = captured.Count == 0
            ? []
            : await db.HinweisBelohnungen.AsNoTracking()
                .Select(r => r.Share!.WantedId)
                .Distinct()
                .ToListAsync(ct);
        var rewardedCaptures = captured.Intersect(rewarded, StringComparer.Ordinal).Count();

        return new PublicKpiRewards(
            rows.Sum(r => r.Amount),
            rows.Where(r => r.Booked).Sum(r => r.Amount),
            rows.Where(r => r.HandedOver).Sum(r => r.Amount),
            rows.Select(r => r.NoticeId).Distinct(StringComparer.Ordinal).Count(),
            captured.Count,
            rewardedCaptures);
    }

    /// <summary>Reaction time of the leadership desk, measured to the first human answer.</summary>
    private static async Task<PublicKpiTickets> TicketsAsync(AppDbContext db, DateTime since, CancellationToken ct)
    {
        var tickets = await db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= since)
            .Select(t => new { t.Id, t.CreatedAt, t.Status })
            .ToListAsync(ct);
        if (tickets.Count == 0)
        {
            return PublicKpiTickets.Empty;
        }

        var ids = tickets.Select(t => t.Id).ToList();
        // one flat query, grouped in memory: a correlated first-reply subquery per ticket is one round trip each
        var replies = await db.TicketNachrichten.AsNoTracking()
            .Where(TicketRules.AgencyRows)
            .Where(m => ids.Contains(m.TicketId))
            .Select(m => new { m.TicketId, m.Audience, m.AuthorIsCitizen, m.CreatedAt })
            .ToListAsync(ct);

        var byTicket = replies.GroupBy(r => r.TicketId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var minutes = new List<int>();
        var waiting = new List<int>();
        var now = DateTime.UtcNow;
        foreach (var ticket in tickets)
        {
            var first = byTicket.TryGetValue(ticket.Id, out var rows)
                ? rows.Where(r => TicketRules.IsHumanAgencyReply(
                        r.Audience, r.AuthorIsCitizen, r.CreatedAt, ticket.CreatedAt))
                    .Select(r => (DateTime?)r.CreatedAt)
                    .DefaultIfEmpty(null)
                    .Min()
                : null;
            if (first is { } answered)
            {
                minutes.Add((int)Math.Max(0, (answered - ticket.CreatedAt).TotalMinutes));
            }
            else if (TicketRules.IsOpen(ticket.Status))
            {
                waiting.Add((int)Math.Max(0, (now - ticket.CreatedAt).TotalMinutes));
            }
        }

        return new PublicKpiTickets(
            tickets.Count,
            minutes.Count,
            waiting.Count,
            // null rather than zero: nothing answered is not an instant answer
            minutes.Count == 0 ? null : Percentile(minutes, 0.5),
            minutes.Count == 0 ? null : Percentile(minutes, 0.95),
            waiting.Count == 0 ? null : waiting.Max());
    }

    /// <summary>Attention drawn by the published notices; null when the reader may not open the cross-list.</summary>
    /// <remarks>
    /// A per-notice figure is fine inside the house — the rule against one is written on the outward record — but a
    /// list that NAMES notices has to pass the same record gate the management list applies, so the person ids are
    /// resolved in a second query and filtered by the viewer's secrecy scope.
    /// </remarks>
    private static async Task<PublicKpiViews?> ViewsAsync(
        AppDbContext db, DateTime since, ClaimsPrincipal actor, CancellationToken ct)
    {
        // the audience of Permission.RequirePublicWantedRead, asked rather than thrown: a reader without it gets no
        // figures at all instead of a zero that would read as "nobody looked"
        if (!actor.MayHighestClassification() && !actor.IsOnlyReader())
        {
            return null;
        }

        var rows = await db.OeffentlicheFahndungen.AsNoTracking()
            .Where(f => f.PublishedAt != null && f.PublishedAt >= since && f.CaseNumber != null)
            .Select(f => new { f.PersonId, CaseNumber = f.CaseNumber!, f.DisplayName, f.ViewCount, f.PublishedAt })
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return new PublicKpiViews(0, 0, 0, 0, []);
        }

        // the record gate as a SECOND query, over the soft-delete filter, exactly as the management list resolves it
        var personIds = rows.Select(r => r.PersonId).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var scope = ViewerScope.From(actor);
        var visible = personIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await db.People.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => personIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.IsClassified, p.IsTRUClassified, p.IsHRBClassified })
                    .ToListAsync(ct))
                .Where(p => RecordVisibility.IsVisible(scope, p.IsClassified, p.IsTRUClassified, p.IsHRBClassified))
                .Select(p => p.Id)
                .ToHashSet(StringComparer.Ordinal);

        var kept = rows.Where(r => r.PersonId is null || visible.Contains(r.PersonId)).ToList();
        if (kept.Count == 0)
        {
            return new PublicKpiViews(0, 0, 0, 0, []);
        }

        var counts = kept.Select(r => r.ViewCount).ToList();
        return new PublicKpiViews(
            counts.Sum(c => (long)c),
            Percentile(counts, 0.5),
            Percentile(counts, 0.9),
            kept.Count,
            kept.OrderByDescending(r => r.ViewCount)
                .Take(TopNotices)
                .Select(r => new PublicKpiNoticeViews(r.CaseNumber, r.DisplayName, r.ViewCount, r.PublishedAt))
                .ToList());
    }

    /// <summary>Nearest-rank percentile over a sample the caller has already checked is non-empty.</summary>
    private static int Percentile(IReadOnlyList<int> values, double fraction)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(fraction * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
