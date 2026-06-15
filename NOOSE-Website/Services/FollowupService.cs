using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IWiedervorlageService" />
public class FollowupService(IDbContextFactory<AppDbContext> dbFactory) : IFollowupService
{
    public async Task<List<FollowupItem>> GetForRecordAsync(string entityType, string entityId,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Wiedervorlagen einer Akte nur zeigen, wenn der Aufrufer die Akte sehen darf (VS/Papierkorb-Gate).
        // Lese-Gate: die Nur-Lese-Aufsicht darf VS-Akten einsehen (DarfVerschlusssacheLesen).
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, actor.MayClassifiedRead(), cancellationToken))
        {
            return new();
        }

        var rows = await db.Followups
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .OrderBy(w => w.Done).ThenBy(w => w.DueAt)
            .Select(w => new
            {
                w.Id, w.DueAt, w.Note, w.ResponsibleAgentId, w.Done, w.DoneAt, w.CreatedById,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        // Zuständigen-Codenamen einsammeln (Codename ist öffentlich, nie Klarname).
        var agentIds = rows.Select(r => r.ResponsibleAgentId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()!;
        var codenames = agentIds.Count == 0
            ? new Dictionary<string, string?>()
            : (await db.Users.Where(u => agentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename })
                .ToListAsync(cancellationToken))
                .ToDictionary(u => u.Id, u => (string?)u.Codename);

        var meId = actor.GetAgentId();
        var isLeadership = actor.IsLeadership();
        var now = DateTime.UtcNow;

        return rows.Select(r => new FollowupItem(
            Id: r.Id,
            DueAt: r.DueAt,
            Note: r.Note,
            ResponsibleAgentId: r.ResponsibleAgentId,
            ResponsibleCodename: r.ResponsibleAgentId is not null && codenames.TryGetValue(r.ResponsibleAgentId, out var cn) ? cn : null,
            Done: r.Done,
            DoneAt: r.DoneAt,
            Overdue: !r.Done && r.DueAt <= now,
            // Nur-Leser (Aufsicht) dürfen nichts bearbeiten – auch nicht eigene/zugewiesene Wiedervorlagen.
            MayEdit: actor.MayWrite() && (isLeadership || r.CreatedById == meId || r.ResponsibleAgentId == meId))).ToList();
    }

    public async Task CreateAsync(string entityType, string entityId, FollowupInput input,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Für diese Akte darf keine Wiedervorlage angelegt werden.");
        }

        var responsibleId = await DetermineResponsibleAsync(db, input.ResponsibleAgentId, actor, cancellationToken);

        db.Followups.Add(new Followup
        {
            EntityType = entityType,
            EntityId = entityId,
            DueAt = input.DueAt.ToUniversalTime(),
            Note = input.Note.TrimToNull(),
            ResponsibleAgentId = responsibleId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshAsync(string id, FollowupInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Followups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        RequireEdit(w, actor);

        var newDue = input.DueAt.ToUniversalTime();
        // Bei verschobenem Termin erneut benachrichtigen lassen (Dedupe-Stempel zurücksetzen).
        if (w.DueAt != newDue)
        {
            w.NotifiedAt = null;
        }
        w.DueAt = newDue;
        w.Note = input.Note.TrimToNull();
        w.ResponsibleAgentId = await DetermineResponsibleAsync(db, input.ResponsibleAgentId, actor, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Followups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        RequireEdit(w, actor);

        if (!w.Done)
        {
            w.Done = true;
            w.DoneAt = DateTime.UtcNow;
            w.DoneById = actor.GetAgentId();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ReopenAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Followups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        RequireEdit(w, actor);

        if (w.Done)
        {
            w.Done = false;
            w.DoneAt = null;
            w.DoneById = null;
            // Erneut fällig → darf wieder gemeldet werden.
            w.NotifiedAt = null;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var w = await db.Followups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Wiedervorlage nicht gefunden.");
        RequireDelete(w, actor);
        db.Followups.Remove(w);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FollowupDashboardItem>> GetMyDueAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var meId = actor.GetAgentId();
        if (string.IsNullOrEmpty(meId))
        {
            return new();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Offen + fällig, und ich bin zuständig ODER folge der Akte (korreliertes EXISTS – auf MySQL/MariaDB zulässig).
        var rows = await db.Followups
            .Where(w => !w.Done && w.DueAt <= now
                && (w.ResponsibleAgentId == meId
                    || db.Watchlists.Any(x => x.AgentId == meId && x.EntityType == w.EntityType && x.EntityId == w.EntityId)))
            .OrderBy(w => w.DueAt)
            .Select(w => new { w.Id, w.EntityType, w.EntityId, w.DueAt, w.Note })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        // Akten-Namen + Href in einer Sammelabfrage; aus Sicht des Aufrufers VS-/Papierkorb-gefiltert.
        // Lese-Gate: die Nur-Lese-Aufsicht darf VS-Akten einsehen.
        var isLeadership = actor.MayClassifiedRead();
        var refs = rows.Select(r => (r.EntityType, r.EntityId)).Distinct().ToList();
        // Taskforces nur auflösen, wenn der Aufrufer zugeteilt ist (oder alle sehen darf) – meId mitgeben.
        var resolved = await RecordsReference.ResolveAsync(db, refs, cancellationToken,
            mayAllTaskforces: actor.MayAllTaskforcesSee(), meId: meId);

        var result = new List<FollowupDashboardItem>();
        foreach (var r in rows)
        {
            if (!resolved.TryGetValue((r.EntityType, r.EntityId), out var a))
            {
                continue; // Akte im Papierkorb/unbekannt → überspringen.
            }
            if (a.Classified && !isLeadership)
            {
                continue; // Verschlusssache für Nicht-Führung verbergen.
            }
            result.Add(new FollowupDashboardItem(r.Id, a.Display, a.Href, r.DueAt, r.Note));
        }
        return result;
    }

    // ---- Helfer ----

    private static async Task<string?> DetermineResponsibleAsync(AppDbContext db, string? desired,
        ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        // Ohne Auswahl: der Ersteller wird zuständig.
        if (string.IsNullOrWhiteSpace(desired))
        {
            return actor.GetAgentId();
        }
        // Mit Auswahl: muss ein existierender, aktiver Agent sein.
        var valid = await db.Users.AnyAsync(u => u.Id == desired && u.Status == AgentStatus.Active, cancellationToken);
        if (!valid)
        {
            throw new InvalidOperationException("Der gewählte zuständige Agent wurde nicht gefunden oder ist nicht aktiv.");
        }
        return desired;
    }

    private static void RequireEdit(Followup w, ClaimsPrincipal actor)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var meId = actor.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && (w.CreatedById == meId || w.ResponsibleAgentId == meId))
        {
            return;
        }
        throw new UnauthorizedAccessException("Diese Wiedervorlage darf nur ihr Ersteller, der Zuständige oder die Führung bearbeiten.");
    }

    private static void RequireDelete(Followup w, ClaimsPrincipal actor)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var meId = actor.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && w.CreatedById == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Eine Wiedervorlage darf nur ihr Ersteller oder die Führung löschen.");
    }
}
