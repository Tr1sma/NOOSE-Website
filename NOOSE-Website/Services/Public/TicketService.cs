using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc />
/// <remarks>
/// Everything either side writes stays plain text on purpose — no rich editor, no HTML, no mention tokens. There is
/// nothing to sanitize, so there is no sanitizer to forget, and the text renders escaped on both sides.
/// </remarks>
public class TicketService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IBuergerService buerger,
    ICaseNumberService caseNumbers,
    INotificationService notifications,
    IPublicTemplateService templates,
    TicketBroadcaster broadcaster) : ITicketService
{
    private const string CaseNumberPrefix = "T";
    private const int ListCap = 200;

    // ---- citizen ----

    public async Task<string> OpenAsync(TicketInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // module first: while the desk is closed, whether this account could open one is nobody else's business
        await modules.RequireEnabledAsync(PublicModules.Tickets, cancellationToken);
        var profile = await buerger.RequireSubmittingCitizenAsync(actor, cancellationToken);
        Permission.RequireWriteAccess(actor);

        var subject = CleanSubject(input.Subject);
        var text = (input.Text ?? string.Empty).Trim();
        if (text.Length < TicketRules.MinLength)
        {
            throw new InvalidOperationException(
                $"Bitte beschreibe dein Anliegen mit mindestens {TicketRules.MinLength} Zeichen.");
        }
        if (text.Length > TicketRules.MaxMessageLength)
        {
            throw new InvalidOperationException(
                $"Eine Nachricht fasst höchstens {TicketRules.MaxMessageLength} Zeichen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // live rows only, unlike the daily cap below: deleting an abusive ticket gives the slot back, while the
        // day's count stays spent
        var open = await db.Tickets.AsNoTracking()
            .Where(t => t.CitizenProfileId == profile.Id)
            .Where(TicketRules.OpenRows)
            .CountAsync(cancellationToken);
        if (open >= TicketRules.MaxOpen)
        {
            throw new InvalidOperationException(
                $"Du hast bereits {TicketRules.MaxOpen} offene Tickets. Bitte warte, bis eines beantwortet ist.");
        }

        // IgnoreQueryFilters on purpose: a deleted ticket still spent its slot, otherwise deleting refills the quota
        var since = DateTime.UtcNow - TicketRules.QuotaWindow;
        var recent = await db.Tickets.IgnoreQueryFilters()
            .CountAsync(t => t.CitizenProfileId == profile.Id && t.CreatedAt >= since, cancellationToken);
        if (recent >= TicketRules.PerDay)
        {
            throw new InvalidOperationException(
                $"Du hast dein Kontingent von {TicketRules.PerDay} Tickets in 24 Stunden erreicht. "
                + "Bitte versuche es später erneut.");
        }

        // read before the transaction, like any other lookup: without an active template no confirmation is
        // written at all, and there is deliberately no fallback text in code
        var confirmation = await templates.GetAutomaticAsync(PublicTemplateKind.TicketEingang, cancellationToken);

        var row = new Ticket
        {
            Kind = TicketArt.Fuehrungsebene,
            CitizenProfileId = profile.Id,
            Subject = subject,
            Status = TicketStatus.Offen,
            LastActivityAt = DateTime.UtcNow,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        row.CaseNumber = await caseNumbers.NextAsync(db, CaseNumberPrefix, cancellationToken);
        db.Tickets.Add(row);
        db.TicketNachrichten.Add(new TicketNachricht
        {
            // the navigation as well as the key: both rows are inserted in one SaveChanges, and this is what
            // orders them. The test database runs with foreign keys off, so only MySQL would have complained
            Ticket = row,
            TicketId = row.Id,
            Audience = TicketMessageAudience.Buerger,
            Text = text,
            AuthorIsCitizen = true,
        });
        if (confirmation is not null)
        {
            // same transaction: a confirmation without a ticket must be impossible. It carries no agent, moves no
            // status (an untouched ticket stays Offen) and rings no bell — the unread counter shows it by itself
            db.TicketNachrichten.Add(new TicketNachricht
            {
                Ticket = row,
                TicketId = row.Id,
                Audience = TicketMessageAudience.Buerger,
                Text = PublicTemplateRenderer.Render(confirmation.Text,
                    new PublicTemplateContext(Name(profile.FirstName, profile.LastName), row.CaseNumber)),
                AuthorIsCitizen = false,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyDeskAsync(db, row, $"Neues Bürger-Ticket {row.CaseNumber}", cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
        return row.CaseNumber;
    }

    /// <inheritdoc />
    /// <remarks>Not module-gated: a citizen keeps reading the tickets they already opened.</remarks>
    public async Task<IReadOnlyList<CitizenTicketRow>> GetOwnAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null)
        {
            // an agent, partner or applicant without a civilian identity sees the page without rows of their own
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Tickets.AsNoTracking()
            .Where(t => t.CitizenProfileId == profile.Id)
            .OrderByDescending(t => t.LastActivityAt)
            .Take(ListCap)
            .Select(t => new
            {
                t.Id,
                t.CaseNumber,
                t.Subject,
                t.Status,
                t.CreatedAt,
                t.LastActivityAt,
                t.CitizenLastReadAt,
            })
            .ToListAsync(cancellationToken);

        var unread = await MessagesByTicketAsync(db, rows.Select(r => r.Id).ToList(), fromCitizen: false,
            cancellationToken);

        return rows
            .Select(r => new CitizenTicketRow(r.CaseNumber, r.Subject, r.Status, r.CreatedAt, r.LastActivityAt,
                UnreadFor(unread, r.Id, r.CitizenLastReadAt)))
            .ToList();
    }

    public async Task<CitizenTicketDetail?> GetOwnDetailAsync(string caseNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(caseNumber))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Tickets.AsNoTracking()
            .Where(t => t.CaseNumber == caseNumber && t.CitizenProfileId == profile.Id)
            .Select(t => new { t.Id, t.CaseNumber, t.Subject, t.Status, t.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var messages = await db.TicketNachrichten.AsNoTracking()
            .Where(m => m.TicketId == row.Id && m.Audience == TicketMessageAudience.Buerger)
            .OrderBy(m => m.CreatedAt)
            // no author is projected because none exists outside; every agency line is the constant sender
            .Select(m => new CitizenTicketMessage(m.CreatedAt, m.Text, m.AuthorIsCitizen))
            .ToListAsync(cancellationToken);

        return new CitizenTicketDetail(row.CaseNumber, row.Subject, row.Status, row.CreatedAt,
            TicketRules.IsOpen(row.Status) && !profile.IsBlocked, profile.IsBlocked, messages);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deliberately not module-gated, unlike <see cref="OpenAsync"/>: the switch stops new concerns, it does not
    /// strand a running conversation. Closed is closed, though — a new concern gets a new ticket.
    /// </remarks>
    public async Task ReplyAsCitizenAsync(string caseNumber, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.RequireSubmittingCitizenAsync(actor, cancellationToken);
        Permission.RequireWriteAccess(actor);

        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Tickets
            .FirstOrDefaultAsync(t => t.CaseNumber == caseNumber && t.CitizenProfileId == profile.Id, cancellationToken)
            ?? throw new InvalidOperationException("Ticket nicht gefunden.");
        if (!TicketRules.IsOpen(row.Status))
        {
            throw new InvalidOperationException(
                "Dieses Ticket ist abgeschlossen. Bitte öffne für ein neues Anliegen ein neues Ticket.");
        }

        db.TicketNachrichten.Add(new TicketNachricht
        {
            TicketId = row.Id,
            Audience = TicketMessageAudience.Buerger,
            Text = body,
            AuthorIsCitizen = true,
        });
        // only this one edge moves: an untouched ticket stays open, because nobody is working it yet
        if (row.Status == TicketStatus.WartetAufBuerger)
        {
            row.Status = TicketStatus.InBearbeitung;
        }
        row.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await NotifyHandlerAsync(db, row, cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    public async Task MarkCitizenReadAsync(string caseNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null || !actor.MayWrite())
        {
            // a read mark is a write; the read-only supervision browsing the citizen area may not set one
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // ExecuteUpdate: reading is not a change to the ticket, and a tracked write would stamp GeaendertAm and log
        // an audit row every time the thread is opened
        await db.Tickets
            .Where(t => t.CaseNumber == caseNumber && t.CitizenProfileId == profile.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CitizenLastReadAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task<int> GetOwnUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null)
        {
            return 0;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var marks = await db.Tickets.AsNoTracking()
            .Where(t => t.CitizenProfileId == profile.Id)
            .Select(t => new { t.Id, t.CitizenLastReadAt })
            .ToListAsync(cancellationToken);
        var unread = await MessagesByTicketAsync(db, marks.Select(m => m.Id).ToList(), fromCitizen: false,
            cancellationToken);
        return marks.Sum(m => UnreadFor(unread, m.Id, m.CitizenLastReadAt));
    }

    // ---- desk (leadership) ----

    public async Task<IReadOnlyList<TicketRow>> GetInboxAsync(TicketInboxScope scope, string? search, bool onlyMine,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // rooted for the same reason as the tip inbox: the projection dereferences the REQUIRED CitizenProfile
        // navigation, so a ticket whose citizen profile was removed vanished from the desk while the tab counter
        // kept counting it
        var query = db.Tickets.IgnoreQueryFilters().AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Where(TicketRules.ScopeFilter(scope));
        if (onlyMine)
        {
            var me = actor.GetAgentId();
            query = query.Where(t => t.HandlerId == me);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            query = query.Where(t => t.CaseNumber.Contains(needle)
                || t.Subject.Contains(needle)
                || t.CitizenProfile!.FirstName.Contains(needle)
                || t.CitizenProfile!.LastName.Contains(needle));
        }

        var rows = await query
            .OrderByDescending(t => t.LastActivityAt)
            .Take(ListCap)
            .Select(t => new
            {
                t.Id,
                t.CaseNumber,
                t.Subject,
                t.Status,
                t.CreatedAt,
                t.LastActivityAt,
                t.AgentLastReadAt,
                FirstName = t.CitizenProfile!.FirstName,
                LastName = t.CitizenProfile!.LastName,
                HandlerCodename = t.Handler!.Codename,
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var fromCitizen = await MessagesByTicketAsync(db, ids, fromCitizen: true, cancellationToken);
        var last = await LastMessageAsync(db, ids, cancellationToken);

        return rows
            .Select(r => new TicketRow(r.Id, r.CaseNumber, r.Subject, r.Status, r.CreatedAt, r.LastActivityAt,
                Name(r.FirstName, r.LastName), r.HandlerCodename,
                last.TryGetValue(r.Id, out var newest) && newest.FromCitizen,
                UnreadFor(fromCitizen, r.Id, r.AgentLastReadAt)))
            .ToList();
    }

    /// <inheritdoc />
    /// <remarks>No guard: the number sits in a badge on a nav entry only leadership renders at all.</remarks>
    public async Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tickets.AsNoTracking().Where(TicketRules.OpenRows).CountAsync(cancellationToken);
    }

    public async Task<TicketDetail?> GetAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Tickets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.CaseNumber,
                t.Subject,
                t.Status,
                t.CreatedAt,
                t.LastActivityAt,
                FirstName = t.CitizenProfile!.FirstName,
                LastName = t.CitizenProfile!.LastName,
                Blocked = t.CitizenProfile!.IsBlocked,
                t.HandlerId,
                HandlerCodename = t.Handler!.Codename,
                t.ClosedAt,
                t.ClosedById,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        // its own lookup rather than a navigation: ClosedById points at the Identity table, and a second include
        // would drag the whole agent row in for one codename
        var closedBy = row.ClosedById is null
            ? null
            : await db.Users.AsNoTracking()
                .Where(u => u.Id == row.ClosedById)
                .Select(u => u.Codename)
                .FirstOrDefaultAsync(cancellationToken);

        return new TicketDetail(row.Id, row.CaseNumber, row.Subject, row.Status, row.CreatedAt, row.LastActivityAt,
            Name(row.FirstName, row.LastName), row.Blocked, row.HandlerId, row.HandlerCodename,
            row.ClosedAt, closedBy);
    }

    public async Task<IReadOnlyList<TicketMessageRow>> GetMessagesAsync(string id, TicketMessageAudience audience,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TicketNachrichten.AsNoTracking()
            .Where(m => m.TicketId == id && m.Audience == audience)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageRow(m.Id, m.Audience, m.Text, m.AuthorIsCitizen,
                m.AuthorAgent!.Codename, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AssignSelfAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        row.HandlerId = actor.GetAgentId();
        if (row.Status == TicketStatus.Offen)
        {
            row.Status = TicketStatus.InBearbeitung;
        }
        row.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    public async Task SetStatusAsync(string id, TicketStatus status, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        if (!TicketRules.IsTransitionAllowed(row.Status, status))
        {
            throw new InvalidOperationException(
                $"Der Wechsel von „{TicketStatusDisplay.Name(row.Status)}“ nach „{TicketStatusDisplay.Name(status)}“ "
                + "ist nicht vorgesehen.");
        }

        row.Status = status;
        row.HandlerId ??= actor.GetAgentId();
        row.LastActivityAt = DateTime.UtcNow;
        // the two fields describe the current closure, not a history of them
        row.ClosedAt = status == TicketStatus.Geschlossen ? DateTime.UtcNow : null;
        row.ClosedById = status == TicketStatus.Geschlossen ? actor.GetAgentId() : null;
        await db.SaveChangesAsync(cancellationToken);

        // only the two statuses that say something to the citizen ring a bell: an internal move from open to
        // handling is news to the desk, not to them
        if (status is TicketStatus.WartetAufBuerger or TicketStatus.Geschlossen)
        {
            await NotifyCitizenAsync(db, row, cancellationToken);
        }
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    public async Task PostInternalNoteAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        db.TicketNachrichten.Add(new TicketNachricht
        {
            TicketId = row.Id,
            Audience = TicketMessageAudience.Intern,
            Text = body,
            AuthorAgentId = actor.GetAgentId(),
        });
        // no activity stamp and no notice: an internal note is not something the citizen is waiting on
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    public async Task ReplyToCitizenAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        if (!TicketRules.IsOpen(row.Status))
        {
            throw new InvalidOperationException("Dieses Ticket ist geschlossen. Bitte öffne es zuerst wieder.");
        }

        db.TicketNachrichten.Add(new TicketNachricht
        {
            TicketId = row.Id,
            Audience = TicketMessageAudience.Buerger,
            Text = body,
            // no author: the citizen-facing row structurally carries no agent, so the outward view has nothing to hide
            AuthorAgentId = null,
        });
        row.HandlerId ??= actor.GetAgentId();
        row.Status = TicketStatus.WartetAufBuerger;
        row.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await NotifyCitizenAsync(db, row, cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    public async Task MarkAgentReadAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketRead(actor);
        if (!actor.MayWrite())
        {
            // the supervision reads the desk but sets no mark; the interceptor would refuse the write anyway
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Tickets.Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AgentLastReadAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        // the interceptor rewrites this into a soft delete; the thread stays with it
        db.Tickets.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    // ---- trash ----

    public async Task<List<Ticket>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tickets.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Tickets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Das Ticket liegt nicht im Papierkorb.");

        // the citizen got the slot back when this was deleted (OpenAsync counts living rows only), so restoring is
        // the second door onto MaxOpen and only the service guards that rule
        if (TicketRules.IsOpen(row.Status))
        {
            var open = await db.Tickets
                .Where(t => t.CitizenProfileId == row.CitizenProfileId)
                .Where(TicketRules.OpenRows)
                .CountAsync(cancellationToken);
            if (open >= TicketRules.MaxOpen)
            {
                throw new InvalidOperationException(
                    $"Dieses Konto hat bereits {TicketRules.MaxOpen} offene Tickets.");
            }
        }

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber);
    }

    // ---- helpers ----

    private static async Task<Ticket> GetOrThrowAsync(AppDbContext db, string id, CancellationToken ct)
        => await db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct)
           ?? throw new InvalidOperationException("Ticket nicht gefunden.");

    private static string Name(string? first, string? last) => $"{first} {last}".Trim();

    private static string CleanSubject(string? value)
    {
        var subject = (value ?? string.Empty).Trim();
        if (subject.Length < TicketRules.SubjectMinLength)
        {
            throw new InvalidOperationException(
                $"Der Betreff braucht mindestens {TicketRules.SubjectMinLength} Zeichen.");
        }
        return subject.Length <= TicketRules.SubjectMaxLength
            ? subject
            : subject[..TicketRules.SubjectMaxLength];
    }

    private static string CleanMessage(string? text)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            throw new InvalidOperationException("Bitte gib einen Text ein.");
        }
        if (body.Length > TicketRules.MaxMessageLength)
        {
            throw new InvalidOperationException(
                $"Eine Nachricht fasst höchstens {TicketRules.MaxMessageLength} Zeichen.");
        }
        return body;
    }

    // one query per page: the timestamps of one side's messages per ticket, so each unread count is computed
    // against that ticket's own read mark
    private static async Task<Dictionary<string, List<DateTime>>> MessagesByTicketAsync(
        AppDbContext db, List<string> ids, bool fromCitizen, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, List<DateTime>>();
        }
        var rows = await db.TicketNachrichten.AsNoTracking()
            .Where(m => ids.Contains(m.TicketId) && m.Audience == TicketMessageAudience.Buerger
                && m.AuthorIsCitizen == fromCitizen)
            .Select(m => new { m.TicketId, m.CreatedAt })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.TicketId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.CreatedAt).ToList());
    }

    // computed in memory: "the newest row of this thread" per ticket is one window function EF would have to
    // invent, and the list is capped at ListCap anyway
    private static async Task<Dictionary<string, (DateTime At, bool FromCitizen)>> LastMessageAsync(
        AppDbContext db, List<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, (DateTime, bool)>();
        }
        var rows = await db.TicketNachrichten.AsNoTracking()
            .Where(m => ids.Contains(m.TicketId) && m.Audience == TicketMessageAudience.Buerger)
            .Select(m => new { m.TicketId, m.CreatedAt, m.AuthorIsCitizen })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.TicketId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    // the audit interceptor stamps one timestamp per SaveChanges, so the opening message and an
                    // automatic confirmation are simultaneous by construction: on a tie the citizen line is the
                    // event, and without this tie-break the newest row would be whatever the database returned first
                    var newest = g.OrderByDescending(r => r.CreatedAt)
                        .ThenByDescending(r => r.AuthorIsCitizen)
                        .First();
                    return (newest.CreatedAt, newest.AuthorIsCitizen);
                });
    }

    private static int UnreadFor(Dictionary<string, List<DateTime>> map, string id, DateTime? readAt)
        => map.TryGetValue(id, out var stamps)
            ? stamps.Count(s => readAt is null || s > readAt)
            : 0;

    private async Task NotifyDeskAsync(AppDbContext db, Ticket row, string title, CancellationToken ct)
    {
        try
        {
            var recipients = await DeskRecipientsAsync(db, ct);
            await notifications.NotifyManyAsync(recipients, NotificationType.PublicTicketOpened, title,
                $"/tickets/{row.Id}", null, ct);
        }
        catch
        {
            /* best effort */
        }
    }

    private async Task NotifyHandlerAsync(AppDbContext db, Ticket row, CancellationToken ct)
    {
        try
        {
            var recipients = row.HandlerId is { Length: > 0 } handler
                ? new List<string> { handler }
                : await DeskRecipientsAsync(db, ct);
            await notifications.NotifyManyAsync(recipients, NotificationType.PublicTicketOpened,
                $"Antwort zum Ticket {row.CaseNumber}", $"/tickets/{row.Id}", null, ct);
        }
        catch
        {
            /* best effort */
        }
    }

    private async Task NotifyCitizenAsync(AppDbContext db, Ticket row, CancellationToken ct)
    {
        try
        {
            var userId = await db.BuergerProfile.AsNoTracking()
                .Where(p => p.Id == row.CitizenProfileId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(ct);
            await notifications.NotifyAsync(userId, NotificationType.PublicTicketAnswered,
                $"Neues zu deinem Ticket {row.CaseNumber}", $"/buerger/tickets/{row.CaseNumber}", ct);
        }
        catch
        {
            /* best effort */
        }
    }

    // leadership hears about a ticket; no other agent sees the desk at all
    private static Task<List<string>> DeskRecipientsAsync(AppDbContext db, CancellationToken ct)
        => db.Users.OnlySelectable()
            .Where(u => u.IsAdmin || u.Rank >= Rank.SupervisorySpecialAgent)
            .Select(u => u.Id)
            .ToListAsync(ct);
}
