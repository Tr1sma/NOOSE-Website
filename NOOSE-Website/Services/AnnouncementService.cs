using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Infrastructure.Announcements;
using NOOSE_Website.Models.Announcements;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAnkuendigungService" />
public class AnnouncementService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseNumberService caseNumber,
    INotificationService notifications,
    AcknowledgmentBroadcaster acknowledgmentBroadcaster) : IAnnouncementService
{
    public async Task<List<AnnouncementRow>> GetBoardAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var meId = actor.GetAgentId();
        var isLeadership = actor.IsLeadership();
        var isTRU = actor.IsTRU();
        var isHRB = actor.IsHRB();
        var myRank = actor.GetRank();

        // Taskforces des Betrachters – für die Sichtbarkeit der Taskforce-Zielgruppe (flaches WHERE FK IN).
        var myTaskforces = string.IsNullOrEmpty(meId)
            ? new List<string>()
            : await db.TaskforceAgents.Where(ta => ta.AgentId == meId)
                .Select(ta => ta.TaskforceId).Distinct().ToListAsync(cancellationToken);

        // Sichtbarkeit = Empfängerkreis ODER Führung (Aufsicht) ODER Verfasser.
        var rows = await db.Announcements
            .Where(a => isLeadership
                || a.CreatedById == meId
                || a.Audience == AnnouncementAudience.AllActive
                || (a.Audience == AnnouncementAudience.Taskforce && a.TargetId != null && myTaskforces.Contains(a.TargetId))
                || (a.Audience == AnnouncementAudience.TruUnit && isTRU)
                || (a.Audience == AnnouncementAudience.HrbUnit && isHRB)
                || (a.Audience == AnnouncementAudience.FromRank && myRank != null
                    && a.MinRank != null && myRank >= a.MinRank))
            .OrderByDescending(a => a.Important)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.CaseNumber, a.Title, a.Content, a.Important, a.Audience, a.TargetId, a.MinRank,
                a.AsBroadcast, a.AcknowledgmentRequired, a.CreatedAt, a.CreatedById,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new();
        }

        var ids = rows.Select(r => r.Id).ToList();

        // Quittierungs-Zeilen (flach) für Betrachter-Status + Zähler – in-memory aggregiert.
        var ack = await db.AnnouncementAcknowledgments
            .Where(q => ids.Contains(q.AnnouncementId))
            .Select(q => new { q.AnnouncementId, q.AgentId, q.AcknowledgedAt })
            .ToListAsync(cancellationToken);
        var ackPerId = ack.GroupBy(q => q.AnnouncementId).ToDictionary(g => g.Key, g => g.ToList());

        // Ersteller-Codenamen (öffentlich, nie Klarname).
        var creatorIds = rows.Select(r => r.CreatedById).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var creatorNames = creatorIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.Where(u => creatorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToDictionaryAsync(u => u.Id, u => u.Codename, cancellationToken);

        // Taskforce-Namen für die Zielgruppen-Anzeige.
        var tfIds = rows.Where(r => r.Audience == AnnouncementAudience.Taskforce && r.TargetId != null)
            .Select(r => r.TargetId!).Distinct().ToList();
        var tfNames = tfIds.Count == 0
            ? new Dictionary<string, string>()
            : await db.Taskforces.IgnoreQueryFilters().Where(t => tfIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return rows.Select(r =>
        {
            var all = ackPerId.TryGetValue(r.Id, out var qs) ? qs : new();
            var my = string.IsNullOrEmpty(meId) ? null : all.FirstOrDefault(x => x.AgentId == meId);
            return new AnnouncementRow
            {
                Id = r.Id,
                CaseNumber = r.CaseNumber,
                Title = r.Title,
                Content = r.Content,
                Important = r.Important,
                Audience = r.Audience,
                TargetDisplay = TargetDisplay(r.Audience, r.TargetId, r.MinRank, tfNames),
                AsBroadcast = r.AsBroadcast,
                AcknowledgmentRequired = r.AcknowledgmentRequired,
                CreatedAt = r.CreatedAt,
                CreatorCodename = r.CreatedById is not null && creatorNames.TryGetValue(r.CreatedById, out var cn) ? cn : null,
                MustAcknowledge = my is { AcknowledgedAt: null },
                AlreadyAcknowledged = my is { AcknowledgedAt: not null },
                AcknowledgedCount = all.Count(x => x.AcknowledgedAt != null),
                TotalCount = all.Count,
                MayManage = isLeadership || r.CreatedById == meId,
            };
        }).ToList();
    }

    public async Task<AnnouncementView?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return null;
        }

        var meId = actor.GetAgentId();
        var isLeadership = actor.IsLeadership();
        var mayManage = isLeadership || a.CreatedById == meId;

        // Sichtbarkeit: Verwalter (Führung/Verfasser) ODER Empfänger der Zielgruppe.
        if (!mayManage
            && !await IsRecipientAsync(db, a, meId, actor.IsTRU(), actor.IsHRB(), actor.GetRank(), cancellationToken))
        {
            return null;
        }

        // Quittierungen (nur wenn verlangt) – Codename für die Verwalter-Liste, Status für den Aufrufer.
        var allAck = a.AcknowledgmentRequired
            ? await db.AnnouncementAcknowledgments.Where(q => q.AnnouncementId == a.Id)
                .Select(q => new { q.AgentId, q.AcknowledgedAt, Codename = q.Agent!.Codename })
                .ToListAsync(cancellationToken)
            : new();
        var my = string.IsNullOrEmpty(meId) ? null : allAck.FirstOrDefault(x => x.AgentId == meId);

        string? creatorCodename = null;
        if (!string.IsNullOrEmpty(a.CreatedById))
        {
            creatorCodename = await db.Users.Where(u => u.Id == a.CreatedById)
                .Select(u => u.Codename).FirstOrDefaultAsync(cancellationToken);
        }

        string targetDisplay;
        if (a.Audience == AnnouncementAudience.Taskforce && a.TargetId != null)
        {
            var tfName = await db.Taskforces.IgnoreQueryFilters()
                .Where(t => t.Id == a.TargetId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);
            targetDisplay = tfName != null ? $"Taskforce: {tfName}" : "Taskforce";
        }
        else
        {
            targetDisplay = TargetDisplay(a.Audience, a.TargetId, a.MinRank, new Dictionary<string, string>());
        }

        var row = new AnnouncementRow
        {
            Id = a.Id,
            CaseNumber = a.CaseNumber,
            Title = a.Title,
            Content = a.Content,
            Important = a.Important,
            Audience = a.Audience,
            TargetDisplay = targetDisplay,
            AsBroadcast = a.AsBroadcast,
            AcknowledgmentRequired = a.AcknowledgmentRequired,
            CreatedAt = a.CreatedAt,
            CreatorCodename = creatorCodename,
            MustAcknowledge = my is { AcknowledgedAt: null },
            AlreadyAcknowledged = my is { AcknowledgedAt: not null },
            AcknowledgedCount = allAck.Count(x => x.AcknowledgedAt != null),
            TotalCount = allAck.Count,
            MayManage = mayManage,
        };

        return new AnnouncementView
        {
            Row = row,
            // Quittierungsliste nur für Verwalter (offene zuerst, dann nach Codename).
            Acknowledgments = mayManage
                ? allAck
                    .OrderBy(x => x.AcknowledgedAt == null ? 0 : 1)
                    .ThenBy(x => x.Codename)
                    .Select(x => new AcknowledgmentRow(x.Codename, x.AcknowledgedAt))
                    .ToList()
                : Array.Empty<AcknowledgmentRow>(),
        };
    }

    public async Task<List<Announcement>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Announcements.IgnoreQueryFilters()
            .Where(a => a.IsDeleted)
            .OrderByDescending(a => a.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Announcement> CreateAsync(AnnouncementInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Broadcast-Features (gezielte Zielgruppe, Push, Quittierung) sind der Führung vorbehalten.
        var isBroadcastFeature = input.AsBroadcast
            || input.Audience != AnnouncementAudience.AllActive
            || input.AcknowledgmentRequired;
        if (isBroadcastFeature)
        {
            Permission.RequireLeadership(actor);
        }

        // Zielgruppen-Parameter validieren.
        if (input.Audience == AnnouncementAudience.Taskforce && string.IsNullOrWhiteSpace(input.TargetId))
        {
            throw new InvalidOperationException("Bitte eine Taskforce als Zielgruppe wählen.");
        }
        if (input.Audience == AnnouncementAudience.FromRank && input.MinRank is null)
        {
            throw new InvalidOperationException("Bitte einen Mindest-Dienstgrad als Zielgruppe wählen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var announcement = new Announcement
        {
            CaseNumber = await caseNumber.NextAsync(db, "N", cancellationToken),
            Title = input.Title.Trim(),
            Content = input.Content?.Trim() ?? string.Empty,
            Important = input.Important,
            Audience = input.Audience,
            TargetId = input.Audience == AnnouncementAudience.Taskforce ? input.TargetId : null,
            MinRank = input.Audience == AnnouncementAudience.FromRank ? input.MinRank : null,
            AsBroadcast = input.AsBroadcast,
            AcknowledgmentRequired = input.AcknowledgmentRequired,
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(cancellationToken);

        // Empfängerkreis nur ermitteln, wenn er gebraucht wird (Quittierung-Snapshot und/oder Push).
        var creatorId = actor.GetAgentId();
        var recipient = announcement.AcknowledgmentRequired || announcement.AsBroadcast
            ? await RecipientIdsAsync(db, announcement, cancellationToken)
            : new List<string>();

        if (announcement.AcknowledgmentRequired)
        {
            // Snapshot der quittierungspflichtigen Empfänger (ohne den Verfasser selbst).
            foreach (var eid in recipient.Distinct().Where(x => x != creatorId))
            {
                db.AnnouncementAcknowledgments.Add(new AnnouncementAcknowledgment
                {
                    AnnouncementId = announcement.Id,
                    AgentId = eid,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        // Glocken-Broadcast nach dem Commit (Verfasser ausgeschlossen, best-effort).
        if (announcement.AsBroadcast)
        {
            await notifications.NotifyManyAsync(recipient, NotificationType.Announcement,
                $"Neue Ankündigung: „{announcement.Title}“.", $"/brett/{announcement.Id}", creatorId, cancellationToken);
        }

        return announcement;
    }

    public async Task RefreshAsync(string id, AnnouncementInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Ankündigung '{id}' nicht gefunden.");
        RequireCreatorOrLeadership(a, actor);

        // Bewusst nur Inhaltliches editierbar – Zielgruppe/Push/Quittierung sind nach dem Anlegen fix
        // (kein Re-Snapshot/Re-Push beim Bearbeiten).
        a.Title = input.Title.Trim();
        a.Content = input.Content?.Trim() ?? string.Empty;
        a.Important = input.Important;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Ankündigung '{id}' nicht gefunden.");
        RequireCreatorOrLeadership(a, actor);
        db.Announcements.Remove(a); // Interceptor wandelt das in Soft-Delete.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.Announcements.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Ankündigung '{id}' nicht gefunden.");

        a.IsDeleted = false;
        a.DeletedAt = null;
        a.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AcknowledgeAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var meId = actor.GetAgentId();
        if (string.IsNullOrEmpty(meId))
        {
            throw new UnauthorizedAccessException("Kein angemeldeter Agent.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.AnnouncementAcknowledgments
            .FirstOrDefaultAsync(q => q.AnnouncementId == id && q.AgentId == meId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Diese Ankündigung erfordert keine Quittierung von dir.");

        if (row.AcknowledgedAt is not null)
        {
            return; // bereits quittiert – idempotent
        }
        row.AcknowledgedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        // Den eigenen NavMenu-Badge des Quittierenden live neu zählen lassen (sonst bliebe er bis zum
        // nächsten kompletten Seiten-Reload eingefroren).
        acknowledgmentBroadcaster.Report(meId);
    }

    public async Task<int> GetOpenAcknowledgmentsCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var meId = actor.GetAgentId();
        if (string.IsNullOrEmpty(meId))
        {
            return 0;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Referenz-Navigation erzwingt den Join auf die Ankündigung → deren Soft-Delete-Filter blendet
        // Papierkorb-Ankündigungen automatisch aus (keine „Geister"-Quittierungen).
        return await db.AnnouncementAcknowledgments
            .CountAsync(q => q.AgentId == meId && q.AcknowledgedAt == null && q.Announcement!.AcknowledgmentRequired, cancellationToken);
    }

    // ---- Helfer ----

    /// <summary>Aktive Agent-Ids des Empfängerkreises einer Ankündigung (für Snapshot/Push).</summary>
    private static async Task<List<string>> RecipientIdsAsync(AppDbContext db, Announcement a, CancellationToken cancellationToken)
    {
        var query = db.Users.Where(u => u.Status == AgentStatus.Active);
        query = a.Audience switch
        {
            AnnouncementAudience.TruUnit => query.Where(u => u.IsTRU),
            AnnouncementAudience.HrbUnit => query.Where(u => u.IsHRB),
            AnnouncementAudience.FromRank => query.Where(u => u.Rank != null && u.Rank >= a.MinRank),
            AnnouncementAudience.Taskforce => query.Where(u => db.TaskforceAgents.Any(ta => ta.TaskforceId == a.TargetId && ta.AgentId == u.Id)),
            _ => query, // AlleAktiven
        };
        return await query.Select(u => u.Id).ToListAsync(cancellationToken);
    }

    /// <summary>Prüft, ob der Aufrufer zum Empfängerkreis einer Ankündigung gehört (Brett-/Detail-Sichtbarkeit).</summary>
    private static async Task<bool> IsRecipientAsync(AppDbContext db, Announcement a, string? meId, bool isTRU,
        bool isHRB, Rank? myRank, CancellationToken cancellationToken)
    {
        switch (a.Audience)
        {
            case AnnouncementAudience.AllActive:
                return true;
            case AnnouncementAudience.TruUnit:
                return isTRU;
            case AnnouncementAudience.HrbUnit:
                return isHRB;
            case AnnouncementAudience.FromRank:
                return myRank != null && a.MinRank != null && myRank >= a.MinRank;
            case AnnouncementAudience.Taskforce:
                return a.TargetId != null && !string.IsNullOrEmpty(meId)
                    && await db.TaskforceAgents.AnyAsync(ta => ta.TaskforceId == a.TargetId && ta.AgentId == meId, cancellationToken);
            default:
                return false;
        }
    }

    private static string TargetDisplay(AnnouncementAudience audience, string? targetId, Rank? minRank,
        IReadOnlyDictionary<string, string> taskforceNames) => audience switch
    {
        AnnouncementAudience.AllActive => "Alle aktiven Agenten",
        AnnouncementAudience.Taskforce => targetId != null && taskforceNames.TryGetValue(targetId, out var n)
            ? $"Taskforce: {n}" : "Taskforce",
        AnnouncementAudience.TruUnit => "TRU-Einheit",
        AnnouncementAudience.HrbUnit => "HRB-Einheit",
        AnnouncementAudience.FromRank => $"Ab {RankDisplay.Name(minRank)}",
        _ => "—",
    };

    private static void RequireCreatorOrLeadership(Announcement a, ClaimsPrincipal actor)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var meId = actor.GetAgentId();
        if (!string.IsNullOrEmpty(meId) && a.CreatedById == meId)
        {
            return;
        }
        throw new UnauthorizedAccessException("Diese Ankündigung darf nur ihr Verfasser oder die Führung bearbeiten.");
    }
}
