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
    IDiscordWebhookService discord,
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
        // not the plain write guard: a partner and the supervision reach the desk the same way a citizen does, and
        // a ticket is correspondence rather than a write into the record stock
        Permission.RequireCitizenSubmission(actor);

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

    // ---- internal ticket (agent) ----

    /// <inheritdoc />
    /// <remarks>
    /// No module gate on purpose: the switch is the OFF button for the citizen desk, not for the agency's own
    /// correspondence. No quota either — the caps count per citizen profile, and there is none here. No automatic
    /// confirmation: that template is written at a citizen and renders BUERGER onto a fallback.
    /// </remarks>
    public async Task<string> OpenAsAgentAsync(TicketInput input, IReadOnlyList<string> participantIds,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // write guard before the rank guard: otherwise the read-only supervision mints a case number and only the
        // interceptor refuses, one step too late
        Permission.RequireWriteAccess(actor);
        Permission.RequireTicketParticipation(actor);

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
        var me = actor.GetAgentId();
        var wanted = participantIds.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal).ToList();
        // AgentSelection is the only source of who may be attached; the write path checks the same predicate the
        // picker uses, or the SignalR path would stay open
        var allowed = wanted.Count == 0
            ? []
            : await db.Users.OnlySelectable()
                .Where(u => wanted.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

        var row = new Ticket
        {
            Kind = TicketArt.Intern,
            CitizenProfileId = null,
            OpenedByAgentId = me,
            Subject = subject,
            Status = TicketStatus.Offen,
            LastActivityAt = DateTime.UtcNow,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        row.CaseNumber = await caseNumbers.NextAsync(db, CaseNumberPrefix, cancellationToken);
        db.Tickets.Add(row);
        db.TicketNachrichten.Add(new TicketNachricht
        {
            Ticket = row,
            TicketId = row.Id,
            // the opening message is the first internal note: an internal ticket has no citizen thread at all
            Audience = TicketMessageAudience.Intern,
            Text = text,
            AuthorAgentId = me,
        });
        // the opener is attached to their own ticket, or they would lose it the moment they leave the page
        foreach (var agentId in allowed.Concat(me is null ? [] : [me]).Distinct(StringComparer.Ordinal))
        {
            db.TicketBeteiligte.Add(new TicketParticipant
            {
                Ticket = row,
                TicketId = row.Id,
                AgentId = agentId,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyDeskAsync(db, row, $"Neues internes Ticket {row.CaseNumber}", cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber, TicketMessageAudience.Intern);
        return row.CaseNumber;
    }

    public async Task<IReadOnlyList<TicketParticipationRow>> GetMyParticipationsAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketParticipation(actor);
        var me = actor.GetAgentId();
        if (string.IsNullOrEmpty(me))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.TicketBeteiligte.AsNoTracking()
            .Where(p => p.AgentId == me)
            .Select(p => new
            {
                p.LastReadAt,
                p.Ticket!.Id,
                p.Ticket!.CaseNumber,
                p.Ticket!.Subject,
                p.Ticket!.Status,
                p.Ticket!.Kind,
                p.Ticket!.LastActivityAt,
                // correlated rather than a second round trip through the ids: the list is short, the count is not
                Unread = db.TicketNachrichten.Count(m => m.TicketId == p.TicketId
                    && m.Audience == TicketMessageAudience.Intern
                    && m.AuthorAgentId != me
                    && (p.LastReadAt == null || m.CreatedAt > p.LastReadAt)),
            })
            .OrderByDescending(r => r.LastActivityAt)
            .Take(ListCap)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TicketParticipationRow(r.Id, r.CaseNumber, r.Subject, r.Status, r.Kind,
                r.LastActivityAt, r.Unread))
            .ToList();
    }

    // ---- participants ----

    public async Task<IReadOnlyList<TicketParticipantRow>> GetParticipantsAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketParticipation(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await TicketVisibility.MayReadAsync(db, id, actor, cancellationToken))
        {
            return [];
        }
        var mayRealName = actor.MayRealNameSee();
        return await db.TicketBeteiligte.AsNoTracking()
            .Where(p => p.TicketId == id)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new TicketParticipantRow(p.Id, p.AgentId, p.Agent!.Codename,
                mayRealName ? p.Agent!.RealName : null, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AddParticipantAsync(string id, string agentId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);

        // the same predicate the picker offers; a raw id off the wire must not get past it
        var selectable = await db.Users.OnlySelectable()
            .AnyAsync(u => u.Id == agentId, cancellationToken);
        if (!selectable)
        {
            throw new InvalidOperationException("Dieser Agent kann nicht beteiligt werden.");
        }
        if (await db.TicketBeteiligte.AnyAsync(p => p.TicketId == id && p.AgentId == agentId, cancellationToken))
        {
            return;
        }

        db.TicketBeteiligte.Add(new TicketParticipant { TicketId = id, AgentId = agentId });
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            // one notice, or the added agent never learns that they are on it
            await notifications.NotifyOnceAsync(agentId, NotificationType.PublicTicketInternal,
                $"Du wurdest am Ticket {row.CaseNumber} beteiligt", $"/tickets/{row.Id}", cancellationToken);
        }
        catch
        {
            /* best effort */
        }
        broadcaster.Report(row.Id, row.CaseNumber, TicketMessageAudience.Intern);
    }

    public async Task RemoveParticipantAsync(string participantId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.TicketBeteiligte
            .Include(p => p.Ticket)
            .FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken);
        if (row is null)
        {
            return;
        }
        // a hard delete: the row IS the permission, and a tombstone that still grants the read is not a removal
        db.TicketBeteiligte.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.TicketId, row.Ticket?.CaseNumber ?? string.Empty, TicketMessageAudience.Intern);
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
            .Select(m => new CitizenTicketMessage(m.CreatedAt, m.Text, m.AuthorIsCitizen, m.ModifiedAt))
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
        // same set that may open one: whoever holds the thread may answer in it
        Permission.RequireCitizenSubmission(actor);

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
        if (profile is null || !actor.MayCitizenSubmit())
        {
            // a read mark is a write, but on the reader's own ticket: whoever may hold the thread moves it, or
            // their unread badge never clears. Only the shared demo principal is out
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

        // rooted for the same reason as the tip inbox: the projection dereferences the CitizenProfile navigation,
        // so a ticket whose citizen profile was removed lost its name on the desk while the tab counter kept
        // counting it. The navigation is optional since internal tickets exist, so nothing here may read a
        // non-nullable value type through it
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
                t.Kind,
                t.CreatedAt,
                t.LastActivityAt,
                t.AgentLastReadAt,
                FirstName = t.CitizenProfile!.FirstName,
                LastName = t.CitizenProfile!.LastName,
                OpenedByCodename = t.OpenedByAgent!.Codename,
                HandlerCodename = t.Handler!.Codename,
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var fromCitizen = await MessagesByTicketAsync(db, ids, fromCitizen: true, cancellationToken);
        var last = await LastMessageAsync(db, ids, cancellationToken);

        return rows
            .Select(r => new TicketRow(r.Id, r.CaseNumber, r.Subject, r.Status, r.Kind, r.CreatedAt, r.LastActivityAt,
                // an internal ticket has no citizen; the column would otherwise be blank on every one of them
                r.Kind == TicketArt.Intern ? r.OpenedByCodename ?? "Intern" : Name(r.FirstName, r.LastName),
                r.HandlerCodename,
                last.TryGetValue(r.Id, out var newest) && newest.FromCitizen,
                UnreadFor(fromCitizen, r.Id, r.AgentLastReadAt)))
            .ToList();
    }

    public async Task<IReadOnlyList<TicketPickRow>> SearchForLinkAsync(string? term, ClaimsPrincipal actor,
        int take = 20, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // the citizen name is deliberately not searchable here, unlike on the desk: this list feeds a link, and a
        // hit by name would put "which citizen writes about this record" into a picker anyone with the desk may open
        var query = db.Tickets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var needle = term.Trim();
            query = query.Where(t => t.CaseNumber.Contains(needle) || t.Subject.Contains(needle));
        }

        return await query
            .OrderByDescending(t => t.LastActivityAt)
            .Take(Math.Clamp(take, 1, ListCap))
            .Select(t => new TicketPickRow(t.Id, t.CaseNumber, t.Subject, t.Status, t.LastActivityAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>No guard: the number sits in a badge on a nav entry only leadership renders at all.</remarks>
    public async Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Tickets.AsNoTracking().Where(TicketRules.OpenRows).CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The set guard is the loose one now: an agent attached to a single ticket may reach that one. Which one is
    /// decided per ticket by <c>TicketVisibility</c>, and a miss returns null rather than throwing — a refusal
    /// would confirm that the ticket exists.
    /// </remarks>
    public async Task<TicketDetail?> GetAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketParticipation(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await TicketVisibility.MayReadAsync(db, id, actor, cancellationToken))
        {
            return null;
        }
        var row = await db.Tickets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.CaseNumber,
                t.Subject,
                t.Status,
                t.Kind,
                t.CreatedAt,
                t.LastActivityAt,
                FirstName = t.CitizenProfile!.FirstName,
                LastName = t.CitizenProfile!.LastName,
                // nullable: an internal ticket has no citizen, and EF unwraps the empty LEFT JOIN into bool
                Blocked = (bool?)t.CitizenProfile!.IsBlocked,
                t.HandlerId,
                HandlerCodename = t.Handler!.Codename,
                OpenedByCodename = t.OpenedByAgent!.Codename,
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

        return new TicketDetail(row.Id, row.CaseNumber, row.Subject, row.Status, row.Kind, row.CreatedAt,
            row.LastActivityAt, Name(row.FirstName, row.LastName), row.Blocked ?? false, row.HandlerId,
            row.HandlerCodename, row.OpenedByCodename, row.ClosedAt, closedBy);
    }

    /// <inheritdoc />
    /// <remarks>
    /// "Internal" used to mean "the whole leadership": the static guard ran and nothing narrowed afterwards. It now
    /// means the desk plus the agents attached to this ticket, decided here rather than in the page — this is the
    /// only read path for messages, so a second page cannot forget it.
    /// </remarks>
    public async Task<IReadOnlyList<TicketMessageRow>> GetMessagesAsync(string id, TicketMessageAudience audience,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketParticipation(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mayRead = audience == TicketMessageAudience.Intern
            ? await TicketVisibility.MayReadInternalAsync(db, id, actor, cancellationToken)
            : await TicketVisibility.MayReadAsync(db, id, actor, cancellationToken);
        if (!mayRead)
        {
            return [];
        }
        // spelled out, not compared against a possibly null id: an unowned row belongs to nobody, and a null
        // actor must not come out as the owner of every line the system ever wrote
        var me = actor.GetAgentId();
        return await db.TicketNachrichten.AsNoTracking()
            .Where(m => m.TicketId == id && m.Audience == audience)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageRow(m.Id, m.Audience, m.Text, m.AuthorIsCitizen,
                m.AuthorAgent!.Codename, m.CreatedAt, m.ModifiedAt,
                me != null && m.CreatedById != null && m.CreatedById == me))
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
        Permission.RequireTicketParticipation(actor);
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await TicketVisibility.MayReadInternalAsync(db, id, actor, cancellationToken) || !actor.MayWrite())
        {
            throw new UnauthorizedAccessException("Du bist an diesem Ticket nicht beteiligt.");
        }
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        var me = actor.GetAgentId();
        db.TicketNachrichten.Add(new TicketNachricht
        {
            TicketId = row.Id,
            Audience = TicketMessageAudience.Intern,
            Text = body,
            AuthorAgentId = me,
        });
        // no activity stamp: an internal note is not something the citizen is waiting on
        await db.SaveChangesAsync(cancellationToken);

        await NotifyInternalAsync(db, row, me, cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber, TicketMessageAudience.Intern);
    }

    public async Task ReplyToCitizenAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketHandling(actor);
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        if (row.Kind == TicketArt.Intern)
        {
            // there is nobody on the other side; the line would be written into a thread no one can open
            throw new InvalidOperationException("Ein internes Ticket hat keinen Bürger-Schriftwechsel.");
        }
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

    /// <inheritdoc />
    /// <remarks>
    /// The audience of the loaded row decides the gate, because the two threads are written under different ones:
    /// an attached agent owns his internal notes, while a line addressed to the citizen belongs to the desk. The
    /// write check runs before either, so the supervision and the demo principal never reach the database.
    ///
    /// A correction, not a message: no status moves, nothing rings, no read mark shifts, and <c>LastActivityAt</c>
    /// stays put — a rewritten line is not something the citizen is waiting on, and it must not push the ticket up
    /// the desk's sort order. A closed ticket stays editable, unlike <see cref="ReplyToCitizenAsync"/>.
    /// </remarks>
    public async Task EditMessageAsync(string messageId, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTicketParticipation(actor);
        if (!actor.MayWrite())
        {
            throw new UnauthorizedAccessException(
                "Bürger-Tickets bearbeitet nur ein schreibberechtigter Agent.");
        }
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.TicketNachrichten
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException("Die Nachricht wurde nicht gefunden.");

        if (message.Audience == TicketMessageAudience.Intern)
        {
            if (!await TicketVisibility.MayReadInternalAsync(db, message.TicketId, actor, cancellationToken))
            {
                throw new UnauthorizedAccessException("Du bist an diesem Ticket nicht beteiligt.");
            }
        }
        else
        {
            Permission.RequireTicketHandling(actor);
        }

        // not covered by the author check below: an account files tickets out of its own civilian identity, and
        // then its own id is the one stamped on the citizen's line
        if (message.AuthorIsCitizen)
        {
            throw new InvalidOperationException("Eine Nachricht des Bürgers kann nicht bearbeitet werden.");
        }
        // author only; an unowned row belongs to nobody, so a null match must not open it
        if (message.CreatedById is null || message.CreatedById != actor.GetAgentId())
        {
            throw new UnauthorizedAccessException("Nur eigene Nachrichten können bearbeitet werden.");
        }
        if (message.Text == body)
        {
            // no audit row and no signal for a save that changes nothing
            return;
        }

        var row = await GetOrThrowAsync(db, message.TicketId, cancellationToken);
        message.Text = body;
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id, row.CaseNumber, message.Audience);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Two marks, two audiences. <c>Ticket.AgentLastReadAt</c> is the DESK's mark, one for the whole house, so only
    /// the desk moves it; an attached agent moves their own row instead, or the "Beteiligt" badge could never clear.
    /// Both go through ExecuteUpdate and therefore past the interceptor, so the write guard is spelled out here.
    /// </remarks>
    public async Task MarkAgentReadAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // participation, not RequireTicketRead: the detail page is open to an attached agent now, and this call
        // sits in its OnParametersSetAsync — the leadership-only guard threw before the page could render
        Permission.RequireTicketParticipation(actor);
        if (!actor.MayWrite())
        {
            // the supervision reads the desk but sets no mark; the interceptor would refuse the write anyway
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await TicketVisibility.MayReadAsync(db, id, actor, cancellationToken))
        {
            return;
        }

        if (TicketVisibility.IsDesk(actor))
        {
            await db.Tickets.Where(t => t.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AgentLastReadAt, DateTime.UtcNow), cancellationToken);
        }

        var me = actor.GetAgentId();
        if (!string.IsNullOrEmpty(me))
        {
            await db.TicketBeteiligte.Where(p => p.TicketId == id && p.AgentId == me)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastReadAt, DateTime.UtcNow), cancellationToken);
        }
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
        // the second door onto MaxOpen and only the service guards that rule. An internal ticket has no citizen:
        // without the null check the comparison becomes "BuergerProfilId IS NULL" and counts every open internal
        // ticket in the house as one account's quota.
        if (TicketRules.IsOpen(row.Status) && row.CitizenProfileId is not null)
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

    /// <summary>Rings the desk on a newly opened ticket and pings the leadership role on Discord.</summary>
    /// <remarks>
    /// All four notifiers go through the folding variants: one bell entry per ticket and category until it is
    /// read, so a running thread is announced once instead of once per line. The Discord ping sits here and not
    /// in the notification service because this notifier runs on the opening only — the desk category itself is
    /// unroutable, so the role is pinged through the category that carries no citizen data: a generic notice plus
    /// the login-gated link, never the subject.
    /// </remarks>
    private async Task NotifyDeskAsync(AppDbContext db, Ticket row, string title, CancellationToken ct)
    {
        try
        {
            var recipients = await DeskRecipientsAsync(db, ct);
            await notifications.NotifyManyOnceAsync(recipients, NotificationType.PublicTicketOpened, title,
                $"/tickets/{row.Id}", null, ct);
            await discord.PushAsync(NotificationType.PublicTicketCreated, $"/tickets/{row.Id}", null,
                cancellationToken: ct);
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
            await notifications.NotifyManyOnceAsync(recipients, NotificationType.PublicTicketOpened,
                $"Antwort zum Ticket {row.CaseNumber}", $"/tickets/{row.Id}", null, ct);
        }
        catch
        {
            /* best effort */
        }
    }

    /// <summary>Only the people actually on the ticket; leadership reads along but is not rung for every note.</summary>
    /// <remarks>Ringing the whole desk on every internal line is how a feature gets muted after a week.</remarks>
    private async Task NotifyInternalAsync(AppDbContext db, Ticket row, string? author, CancellationToken ct)
    {
        try
        {
            var recipients = await db.TicketBeteiligte.AsNoTracking()
                .Where(p => p.TicketId == row.Id)
                .Select(p => p.AgentId)
                .ToListAsync(ct);
            if (row.HandlerId is { Length: > 0 } handler)
            {
                recipients.Add(handler);
            }
            var targets = recipients
                .Where(x => !string.IsNullOrEmpty(x) && x != author)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }
            await notifications.NotifyManyOnceAsync(targets, NotificationType.PublicTicketInternal,
                $"Interne Notiz zum Ticket {row.CaseNumber}", $"/tickets/{row.Id}", null, ct);
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
            await notifications.NotifyOnceAsync(userId, NotificationType.PublicTicketAnswered,
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
