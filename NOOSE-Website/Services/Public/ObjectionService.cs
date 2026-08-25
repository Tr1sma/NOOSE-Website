using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Cases;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc />
/// <remarks>
/// Plain text on both sides, like every other citizen channel: nothing to sanitize, so there is no sanitizer to
/// forget. The case is opened through <see cref="ICaseService"/> rather than built here, so its case-number
/// transaction and classification gate keep working — the pattern <c>TipTakeoverService</c> set.
/// </remarks>
public class ObjectionService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IBuergerService buerger,
    ICaseNumberService caseNumbers,
    INotificationService notifications,
    IPublicWantedService wanted,
    ICaseService cases) : IObjectionService
{
    private const string CaseNumberPrefix = "EIN";
    private const int ListCap = 200;
    private const string NotFound = "Einspruch nicht gefunden.";

    // ---- citizen ----

    public async Task<string> SubmitAsync(ObjectionInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // module first: while objections are closed, whether this account could file one is nobody else's business
        await modules.RequireEnabledAsync(PublicModules.Objection, cancellationToken);
        var profile = await buerger.RequireSubmittingCitizenAsync(actor, cancellationToken);
        Permission.RequireWriteAccess(actor);

        var text = (input.Text ?? string.Empty).Trim();
        if (text.Length < ObjectionRules.MinLength)
        {
            throw new InvalidOperationException(
                $"Bitte begründe deinen Einspruch mit mindestens {ObjectionRules.MinLength} Zeichen.");
        }
        if (text.Length > ObjectionRules.MaxLength)
        {
            throw new InvalidOperationException($"Ein Einspruch fasst höchstens {ObjectionRules.MaxLength} Zeichen.");
        }

        // resolved through the public read path, never through a row id from outside: that path sits behind the
        // suppression belt, so a draft or a retracted notice reads as "does not exist"
        var notice = await wanted.GetByCaseNumberAsync(input.WantedCaseNumber, cancellationToken)
            ?? throw new InvalidOperationException("Zu diesem Aktenzeichen gibt es keine laufende Ausschreibung.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var wantedId = await db.OeffentlicheFahndungen.AsNoTracking()
            .Where(f => f.CaseNumber == notice.CaseNumber)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Zu diesem Aktenzeichen gibt es keine laufende Ausschreibung.");

        // IgnoreQueryFilters on purpose: a deleted objection still spent its slot, otherwise deleting refills it
        var since = DateTime.UtcNow - ObjectionRules.QuotaWindow;
        var recent = await db.FahndungEinsprueche.IgnoreQueryFilters()
            .CountAsync(e => e.CitizenProfileId == profile.Id && e.CreatedAt >= since, cancellationToken);
        if (recent >= ObjectionRules.PerDay)
        {
            throw new InvalidOperationException(
                $"Du hast dein Kontingent von {ObjectionRules.PerDay} Einsprüchen in 24 Stunden erreicht. "
                + "Bitte versuche es später erneut.");
        }

        // living rows only, unlike the daily cap: a decided objection frees the notice for a new one, and deleting
        // an abusive objection gives the slot back
        var open = await db.FahndungEinsprueche.AsNoTracking()
            .Where(e => e.CitizenProfileId == profile.Id && e.WantedId == wantedId)
            .Where(ObjectionRules.OpenRows)
            .AnyAsync(cancellationToken);
        if (open)
        {
            throw new InvalidOperationException("Zu dieser Ausschreibung läuft bereits ein Einspruch von dir.");
        }

        var row = new FahndungEinspruch
        {
            WantedId = wantedId,
            CitizenProfileId = profile.Id,
            Text = text,
            Status = ObjectionStatus.Neu,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        row.CaseNumber = await caseNumbers.NextAsync(db, CaseNumberPrefix, cancellationToken);
        db.FahndungEinsprueche.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyDeskAsync(db, notice.CaseNumber, row.CaseNumber, cancellationToken);
        return row.CaseNumber;
    }

    /// <inheritdoc />
    /// <remarks>Not module-gated: a citizen keeps reading the objections they already filed.</remarks>
    public async Task<IReadOnlyList<CitizenObjectionRow>> GetOwnAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null)
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // IgnoreQueryFilters at the root, then !IsDeleted written out again. It is compilation-scoped, so it also
        // lifts the filter off the Wanted navigation — which is exactly what is wanted here: a citizen must still
        // see what they objected to after the notice was retracted or deleted. Their own deleted objection stays
        // hidden, hence the explicit clause. The name comes from the publication snapshot, never from the file.
        return await db.FahndungEinsprueche
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.CitizenProfileId == profile.Id && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new CitizenObjectionRow(
                e.CaseNumber,
                e.Wanted!.CaseNumber ?? string.Empty,
                e.Wanted.DisplayName,
                e.Status,
                e.Text,
                e.DecisionNote,
                e.CreatedAt,
                e.DecidedAt))
            .ToListAsync(cancellationToken);
    }

    // ---- desk ----

    public async Task<IReadOnlyList<ObjectionRow>> GetListAsync(bool onlyOpen, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.FahndungEinsprueche.AsNoTracking();
        query = onlyOpen
            ? query.Where(ObjectionRules.OpenRows)
            : query.Where(e => e.Status == ObjectionStatus.Angenommen || e.Status == ObjectionStatus.Abgelehnt);

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(ListCap)
            .Select(e => new ObjectionRow(
                e.Id,
                e.CaseNumber,
                e.Wanted!.CaseNumber ?? string.Empty,
                e.Wanted.DisplayName,
                e.Wanted.Status,
                e.Status,
                e.CitizenProfile!.FirstName + " " + e.CitizenProfile.LastName,
                e.DecidedBy!.Codename,
                e.CreatedAt,
                e.DecidedAt,
                e.LinkedCaseId != null))
            .ToListAsync(cancellationToken);
    }

    public async Task<ObjectionCounts> GetCountsAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // one grouped pass, so the tabs and their numbers cannot disagree
        var rows = await db.FahndungEinsprueche.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new ObjectionCounts(
            rows.Where(r => ObjectionRules.IsOpen(r.Status)).Sum(r => r.Count),
            rows.Where(r => !ObjectionRules.IsOpen(r.Status)).Sum(r => r.Count));
    }

    public async Task<ObjectionDetail?> GetAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FahndungEinsprueche.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new ObjectionDetail(
                e.Id,
                e.CaseNumber,
                e.WantedId,
                e.Wanted!.CaseNumber ?? string.Empty,
                e.Wanted.DisplayName,
                e.Wanted.Status,
                e.Status,
                e.Text,
                e.CitizenProfile!.FirstName + " " + e.CitizenProfile.LastName,
                e.CitizenProfile.IsBlocked,
                e.DecisionNote,
                e.DecidedBy!.Codename,
                e.CreatedAt,
                e.DecidedAt,
                e.LinkedCaseId,
                e.LinkedCase!.CaseNumber))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetStatusAsync(string id, ObjectionStatus status, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionHandling(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.FahndungEinsprueche.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        if (!ObjectionRules.IsTransitionAllowed(row.Status, status))
        {
            throw new InvalidOperationException("Dieser Schritt ist von hier aus nicht vorgesehen.");
        }

        var decided = status is ObjectionStatus.Angenommen or ObjectionStatus.Abgelehnt;
        var clean = (note ?? string.Empty).Trim();
        if (decided && clean.Length == 0)
        {
            throw new InvalidOperationException("Zu einer Entscheidung gehört eine Begründung — der Bürger liest sie.");
        }
        if (clean.Length > ObjectionRules.MaxNoteLength)
        {
            throw new InvalidOperationException(
                $"Die Begründung fasst höchstens {ObjectionRules.MaxNoteLength} Zeichen.");
        }

        if (status == ObjectionStatus.Angenommen)
        {
            await RequireNoticeOfflineAsync(db, row.WantedId, cancellationToken);
        }

        row.Status = status;
        if (decided)
        {
            row.DecisionNote = clean;
            row.DecidedById = actor.GetAgentId();
            row.DecidedAt = DateTime.UtcNow;
        }
        else
        {
            // back into review: these fields describe the current decision, not its history
            row.DecisionNote = null;
            row.DecidedById = null;
            row.DecidedAt = null;
        }
        await db.SaveChangesAsync(cancellationToken);

        if (decided)
        {
            await NotifyCitizenAsync(db, row, cancellationToken);
        }
    }

    public async Task<string> ToCaseAsync(string id, string? title, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionHandling(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.FahndungEinsprueche.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        if (row.LinkedCaseId is { Length: > 0 })
        {
            throw new InvalidOperationException("Zu diesem Einspruch gibt es bereits einen Vorgang.");
        }

        // opened through its own service, never built here: its case-number transaction, its classification gate
        // and its audit row all keep working
        var @case = await cases.CreateAsync(new CaseInput
        {
            Title = string.IsNullOrWhiteSpace(title) ? $"Einspruch {row.CaseNumber}" : title.Trim(),
            Status = CaseStatus.Open,
            Classification = Classification.Unknown,
            SecrecyLevel = DocumentClassification.None,
        }, actor, cancellationToken);

        // Compare-and-swap, not a tracked write: two tabs would otherwise open two cases and the last writer would
        // win, leaving one of them orphaned with nothing pointing at it.
        var claimed = await db.FahndungEinsprueche
            .Where(e => e.Id == id && e.LinkedCaseId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LinkedCaseId, @case.Id), cancellationToken);
        if (claimed == 0)
        {
            await DiscardCaseAsync(@case.Id, actor, cancellationToken);
            throw new InvalidOperationException("Zu diesem Einspruch wurde soeben ein Vorgang angelegt.");
        }

        // ExecuteUpdate bypasses the audit interceptor, so the link is recorded by hand
        db.AuditLogs.Add(ManualAudit.Row(nameof(FahndungEinspruch), id, AuditAction.Modified, actor,
            ManualAudit.Change("Vorgang", null, @case.CaseNumber)));
        await db.SaveChangesAsync(cancellationToken);
        return @case.Id;
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionHandling(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.FahndungEinsprueche.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);

        // the audit interceptor rewrites this into a soft delete
        db.FahndungEinsprueche.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- trash ----

    public async Task<List<FahndungEinspruch>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FahndungEinsprueche
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => e.IsDeleted)
            .OrderByDescending(e => e.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireObjectionHandling(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.FahndungEinsprueche
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id && e.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Der Einspruch liegt nicht im Papierkorb.");

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---- internals ----

    /// <summary>Upholding an objection needs the notice already offline.</summary>
    /// <remarks>
    /// Phase 9's rule, applied here: "Gefasst is a precondition, not a side effect". Retracting demands a reason,
    /// and a reason a human chose beats one this service would invent — so the notice is pulled first, deliberately,
    /// and only then can the objection be upheld. That also keeps the wanted table at its single writer.
    /// No IgnoreQueryFilters worry: this is a standalone query, so widening it widens nothing else.
    /// </remarks>
    private static async Task RequireNoticeOfflineAsync(AppDbContext db, string wantedId,
        CancellationToken cancellationToken)
    {
        var status = await db.OeffentlicheFahndungen
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.Id == wantedId)
            .Select(f => (PublicWantedStatus?)f.Status)
            .FirstOrDefaultAsync(cancellationToken);
        // fail closed: a notice that does not resolve at all is not demonstrably offline
        if (status is null || PublicWantedService.PubliclyVisible.Contains(status.Value))
        {
            throw new InvalidOperationException(
                "Die Ausschreibung ist noch öffentlich. Zieh sie zuerst mit Begründung zurück, dann lässt sich dem "
                + "Einspruch stattgeben.");
        }
    }

    private async Task DiscardCaseAsync(string caseId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Cases.Where(v => v.Id == caseId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.IsDeleted, true)
                .SetProperty(v => v.DeletedAt, DateTime.UtcNow)
                .SetProperty(v => v.DeletedById, actor.GetAgentId()), cancellationToken);
        await db.CaseAgents.Where(a => a.CaseId == caseId).ExecuteDeleteAsync(cancellationToken);
        db.AuditLogs.Add(ManualAudit.Row(nameof(Case), caseId, AuditAction.Deleted, actor,
            ManualAudit.Change("Verworfen", null, "Doppelter Vorgang zu einem Einspruch")));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Leadership hears about a fresh objection; PublicObjectionReceived is deliberately not routable.</summary>
    private async Task NotifyDeskAsync(AppDbContext db, string wantedCaseNumber, string caseNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var recipients = await db.Users.OnlySelectable()
                .Where(u => u.IsAdmin || u.Rank >= Rank.SupervisorySpecialAgent)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            await notifications.NotifyManyAsync(recipients, NotificationType.PublicObjectionReceived,
                $"Einspruch {caseNumber} gegen Ausschreibung {wantedCaseNumber}", "/fahndung?tab=einsprueche", null,
                cancellationToken);
        }
        catch
        {
            /* best effort */
        }
    }

    private async Task NotifyCitizenAsync(AppDbContext db, FahndungEinspruch row, CancellationToken cancellationToken)
    {
        try
        {
            var userId = await db.BuergerProfile.AsNoTracking()
                .Where(p => p.Id == row.CitizenProfileId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            await notifications.NotifyAsync(userId, NotificationType.PublicObjectionDecided,
                $"Entscheidung zu deinem Einspruch {row.CaseNumber}", "/buerger/einspruch", cancellationToken);
        }
        catch
        {
            /* best effort */
        }
    }
}
