using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc />
/// <remarks>
/// Everything a citizen writes stays plain text on purpose — no rich editor, no HTML, no mention tokens. There is
/// nothing to sanitize, so there is no sanitizer to forget, and the text renders escaped on both sides.
/// </remarks>
public class TipService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IBuergerService buerger,
    IPublicWantedService wanted,
    ICaseNumberService caseNumbers,
    ITipAttachmentStorageService storage,
    INotificationService notifications,
    ITipPriorityService priority,
    IPublicTemplateService templates,
    TipsBroadcaster broadcaster) : ITipService
{
    private const string CaseNumberPrefix = "H";
    private const int ListCap = 200;
    private const int ExcerptLength = 160;

    // ---- citizen ----

    public async Task<string> SubmitAsync(TipInput input, Stream? attachment, string? contentType,
        string? originalName, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // module first: whether this account could submit is none of the caller's business while the desk is closed
        await modules.RequireEnabledAsync(PublicModules.Tips, cancellationToken);
        var profile = await buerger.RequireSubmittingCitizenAsync(actor, cancellationToken);
        Permission.RequireWriteAccess(actor);

        var text = (input.Text ?? string.Empty).Trim();
        if (text.Length < TipRules.MinLength)
        {
            throw new InvalidOperationException(
                $"Bitte beschreibe deine Beobachtung mit mindestens {TipRules.MinLength} Zeichen.");
        }
        if (text.Length > TipRules.MaxLength)
        {
            throw new InvalidOperationException($"Ein Hinweis fasst höchstens {TipRules.MaxLength} Zeichen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // IgnoreQueryFilters on purpose: a deleted tip still spent its slot, otherwise deleting refills the quota
        var since = DateTime.UtcNow - TipRules.QuotaWindow;
        var recent = await db.Hinweise.IgnoreQueryFilters()
            .CountAsync(h => h.CitizenProfileId == profile.Id && h.CreatedAt >= since, cancellationToken);
        var quota = TipTrust.QuotaFor(profile.ConfirmedTips);
        if (recent >= quota)
        {
            throw new InvalidOperationException(
                $"Du hast dein Kontingent von {quota} Hinweisen in 24 Stunden erreicht. "
                + "Bitte versuche es später erneut.");
        }

        var wantedId = await ResolveNoticeAsync(db, input.WantedCaseNumber, cancellationToken);

        // saved before the transaction opens, exactly like a recruiting attachment: a stream copy inside a
        // transaction holds a database connection open for the length of an upload
        string? fileName = null;
        if (attachment is not null)
        {
            if (string.IsNullOrWhiteSpace(contentType) || !storage.IsAllowedType(contentType))
            {
                throw new InvalidOperationException("Als Anhang sind nur Bilder (JPG, PNG, WEBP, GIF) erlaubt.");
            }
            fileName = await storage.SaveAsync(attachment, contentType, cancellationToken);
        }

        // read before the transaction, like the attachment above: without an active template no confirmation is
        // written at all, and there is deliberately no fallback text in code
        var confirmation = await templates.GetAutomaticAsync(PublicTemplateKind.HinweisEingang, cancellationToken);

        var row = new Hinweis
        {
            CitizenProfileId = profile.Id,
            WantsAnonymity = input.WantsAnonymity,
            WantedId = wantedId,
            Text = text,
            AttachmentFileName = fileName,
            AttachmentOriginalName = fileName is null ? null : Trim(originalName, 255),
            AttachmentContentType = fileName is null ? null : contentType,
            Status = TipStatus.Neu,
            Priority = await priority.ComputeAsync(db, wantedId, profile.ConfirmedTips, cancellationToken),
        };

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            row.CaseNumber = await caseNumbers.NextAsync(db, CaseNumberPrefix, cancellationToken);
            db.Hinweise.Add(row);
            if (confirmation is not null)
            {
                // same transaction: a confirmation without a tip must be impossible. It carries no agent, leaves the
                // status at Neu and rings no bell — the unread counter shows it by itself. Under the anonymity
                // promise the renderer gets no name, so the salutation falls back instead of leaking one
                var salutation = TipAnonymity.IsHidden(row.WantsAnonymity, row.AnonymityResolvedAt)
                    ? null
                    : Name(profile.FirstName, profile.LastName);
                db.HinweisNachrichten.Add(new HinweisNachricht
                {
                    Hinweis = row,
                    HinweisId = row.Id,
                    Audience = TipMessageAudience.Buerger,
                    Text = PublicTemplateRenderer.Render(confirmation.Text,
                        new PublicTemplateContext(salutation, row.CaseNumber)),
                    AuthorIsCitizen = false,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // the copy is the only side effect a rollback does not undo
            if (fileName is not null)
            {
                try { storage.Delete(fileName); } catch { /* best effort */ }
            }
            throw;
        }

        await GroupDuplicatesAsync(db, row, cancellationToken);
        await NotifyDeskAsync(db, row, cancellationToken);
        broadcaster.Report(row.Id);
        return row.CaseNumber;
    }

    /// <inheritdoc />
    /// <remarks>Not module-gated either: a citizen keeps reading the tips they already filed.</remarks>
    public async Task<IReadOnlyList<CitizenTipRow>> GetOwnAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null)
        {
            // an agent, partner or applicant without a civilian identity sees the page without rows of their own
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Hinweise.AsNoTracking()
            .Where(h => h.CitizenProfileId == profile.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(ListCap)
            .Select(h => new
            {
                h.Id,
                h.CaseNumber,
                h.Status,
                h.CreatedAt,
                h.Text,
                h.CitizenLastReadAt,
                WantedCaseNumber = h.Wanted!.CaseNumber,
                WantedDisplayName = h.Wanted!.DisplayName,
                HasAttachment = h.AttachmentFileName != null,
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var unread = await UnreadByTipAsync(db, ids, cancellationToken);

        return rows
            .Select(r => new CitizenTipRow(r.CaseNumber, r.Status, r.CreatedAt, Excerpt(r.Text),
                r.WantedCaseNumber, r.WantedDisplayName, r.HasAttachment,
                UnreadFor(unread, r.Id, r.CitizenLastReadAt)))
            .ToList();
    }

    public async Task<CitizenTipDetail?> GetOwnDetailAsync(string caseNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(caseNumber))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Hinweise.AsNoTracking()
            .Where(h => h.CaseNumber == caseNumber && h.CitizenProfileId == profile.Id)
            .Select(h => new
            {
                h.Id,
                h.CaseNumber,
                h.Status,
                h.CreatedAt,
                h.Text,
                h.WantsAnonymity,
                h.AnonymityResolvedAt,
                h.AttachmentFileName,
                h.AttachmentOriginalName,
                WantedCaseNumber = h.Wanted!.CaseNumber,
                WantedDisplayName = h.Wanted!.DisplayName,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var messages = await db.HinweisNachrichten.AsNoTracking()
            .Where(m => m.HinweisId == row.Id && m.Audience == TipMessageAudience.Buerger)
            .OrderBy(m => m.CreatedAt)
            // no author is projected because none may exist outside; the agency answers as NOOSE
            .Select(m => new CitizenTipMessage(m.CreatedAt, m.Text, m.AuthorIsCitizen))
            .ToListAsync(cancellationToken);

        return new CitizenTipDetail(row.CaseNumber, row.Status, row.CreatedAt, row.Text, row.WantsAnonymity,
            row.AnonymityResolvedAt is not null, row.WantedCaseNumber, row.WantedDisplayName,
            row.AttachmentFileName is not null, row.AttachmentOriginalName,
            !TipRules.IsClosed(row.Status) && !profile.IsBlocked, messages);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deliberately not module-gated, unlike <see cref="SubmitAsync"/>: the switch stops new tips, it does not strand
    /// a running one. Same call as with the citizen registration — an existing case keeps its way in, or one flipped
    /// switch locks people out of a conversation the agency itself started.
    /// </remarks>
    public async Task ReplyAsCitizenAsync(string caseNumber, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await buerger.RequireSubmittingCitizenAsync(actor, cancellationToken);
        Permission.RequireWriteAccess(actor);

        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Hinweise
            .FirstOrDefaultAsync(h => h.CaseNumber == caseNumber && h.CitizenProfileId == profile.Id, cancellationToken)
            ?? throw new InvalidOperationException("Hinweis nicht gefunden.");
        if (TipRules.IsClosed(row.Status))
        {
            throw new InvalidOperationException("Dieser Hinweis ist abgeschlossen.");
        }

        db.HinweisNachrichten.Add(new HinweisNachricht
        {
            HinweisId = row.Id,
            Audience = TipMessageAudience.Buerger,
            Text = body,
            AuthorIsCitizen = true,
        });
        await db.SaveChangesAsync(cancellationToken);

        await NotifyHandlerAsync(db, row, cancellationToken);
        broadcaster.Report(row.Id);
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
        // ExecuteUpdate: a read mark is not a change to the tip, and a tracked write would stamp GeaendertAm and
        // push the tip onto the file's timeline every time the citizen opens it
        await db.Hinweise
            .Where(h => h.CaseNumber == caseNumber && h.CitizenProfileId == profile.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.CitizenLastReadAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task<int> GetOwnUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var profile = await buerger.GetOwnAsync(actor, cancellationToken);
        if (profile is null)
        {
            return 0;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var marks = await db.Hinweise.AsNoTracking()
            .Where(h => h.CitizenProfileId == profile.Id)
            .Select(h => new { h.Id, h.CitizenLastReadAt })
            .ToListAsync(cancellationToken);
        var unread = await UnreadByTipAsync(db, marks.Select(m => m.Id).ToList(), cancellationToken);
        return marks.Sum(m => UnreadFor(unread, m.Id, m.CitizenLastReadAt));
    }

    // ---- handler ----

    public async Task<IReadOnlyList<TipRow>> GetInboxAsync(TipInboxScope scope, string? search, bool onlyMine,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Hinweise.AsNoTracking().Where(ScopeFilter(scope));
        if (onlyMine)
        {
            var me = actor.GetAgentId();
            query = query.Where(h => h.HandlerId == me);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(h => h.CaseNumber.Contains(term) || h.Text.Contains(term));
        }

        var rows = await query
            .OrderByDescending(h => h.Priority).ThenByDescending(h => h.CreatedAt)
            .Take(ListCap)
            .Select(h => new
            {
                h.Id,
                h.CaseNumber,
                h.Status,
                h.CreatedAt,
                h.Text,
                h.WantsAnonymity,
                h.AnonymityResolvedAt,
                CitizenFirstName = h.CitizenProfile!.FirstName,
                CitizenLastName = h.CitizenProfile!.LastName,
                WantedCaseNumber = h.Wanted!.CaseNumber,
                WantedDisplayName = h.Wanted!.DisplayName,
                HasAttachment = h.AttachmentFileName != null,
                HandlerCodename = h.Handler!.Codename,
                h.Priority,
                h.DuplicateGroupId,
                ConfirmedTips = h.CitizenProfile!.ConfirmedTips,
            })
            .ToListAsync(cancellationToken);

        var last = await LastCitizenMessageAsync(db, rows.Select(r => r.Id).ToList(), cancellationToken);
        var duplicates = await DuplicateCountsAsync(db,
            rows.Select(r => r.DuplicateGroupId).OfType<string>().Distinct().ToList(), cancellationToken);

        return rows
            .Select(r => new TipRow(r.Id, r.CaseNumber, r.Status, r.CreatedAt, Excerpt(r.Text),
                IsHidden(r.WantsAnonymity, r.AnonymityResolvedAt),
                IsHidden(r.WantsAnonymity, r.AnonymityResolvedAt)
                    ? null
                    : Name(r.CitizenFirstName, r.CitizenLastName),
                r.WantedCaseNumber, r.WantedDisplayName, r.HasAttachment, r.HandlerCodename,
                last.TryGetValue(r.Id, out var m) ? m.At : (DateTime?)null,
                last.TryGetValue(r.Id, out var m2) && m2.FromCitizen,
                r.Priority, TipTrust.Tier(r.ConfirmedTips), r.DuplicateGroupId,
                r.DuplicateGroupId is null ? 0 : duplicates.GetValueOrDefault(r.DuplicateGroupId)))
            .ToList();
    }

    public async Task<TipInboxCounts> GetCountsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var byStatus = await db.Hinweise.AsNoTracking()
            .GroupBy(h => h.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Sum(Func<TipStatus, bool> match) => byStatus.Where(x => match(x.Status)).Sum(x => x.Count);
        return new TipInboxCounts(
            Sum(s => s == TipStatus.Neu),
            Sum(s => s is TipStatus.InPruefung or TipStatus.Rueckfrage),
            Sum(TipRules.IsClosed));
    }

    /// <inheritdoc />
    /// <remarks>
    /// No actor: the navigation reads it for whoever the drawer is being drawn for, and the entry itself is already
    /// policy-gated. One number about the agency's own workload leaves nothing behind. Same shape as the request badge.
    /// </remarks>
    public async Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Hinweise.AsNoTracking().CountAsync(h => h.Status == TipStatus.Neu, cancellationToken);
    }

    public async Task<TipDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Hinweise.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => new
            {
                h.Id,
                h.CaseNumber,
                h.Status,
                h.CreatedAt,
                h.Text,
                h.WantsAnonymity,
                h.AnonymityResolvedAt,
                h.AnonymityResolvedById,
                CitizenFirstName = h.CitizenProfile!.FirstName,
                CitizenLastName = h.CitizenProfile!.LastName,
                CitizenConfirmedTips = (int?)h.CitizenProfile!.ConfirmedTips,
                WantedCaseNumber = h.Wanted!.CaseNumber,
                WantedDisplayName = h.Wanted!.DisplayName,
                h.AttachmentFileName,
                h.AttachmentOriginalName,
                h.HandlerId,
                HandlerCodename = h.Handler!.Codename,
                h.Priority,
                h.DuplicateGroupId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var hidden = IsHidden(row.WantsAnonymity, row.AnonymityResolvedAt);
        string? resolvedBy = null;
        if (row.AnonymityResolvedById is not null)
        {
            resolvedBy = await db.Users.AsNoTracking()
                .Where(u => u.Id == row.AnonymityResolvedById)
                .Select(u => u.Codename)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new TipDetail(row.Id, row.CaseNumber, row.Status, row.CreatedAt, row.Text, row.WantsAnonymity,
            row.AnonymityResolvedAt is not null, row.AnonymityResolvedAt, resolvedBy,
            hidden ? null : Name(row.CitizenFirstName, row.CitizenLastName),
            hidden ? null : row.CitizenConfirmedTips,
            row.WantedCaseNumber, row.WantedDisplayName,
            row.AttachmentFileName is not null, row.AttachmentOriginalName,
            row.HandlerId, row.HandlerCodename,
            row.Priority, TipTrust.Tier(row.CitizenConfirmedTips ?? 0), row.DuplicateGroupId);
    }

    public async Task<IReadOnlyList<TipNoticeRow>> GetForNoticeAsync(string wantedId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // no citizen projection, so no dereference of the required profile navigation — which would INNER-join the
        // tips of a removed citizen out of a list that is supposed to describe the notice, not the tipsters
        var rows = await db.Hinweise.AsNoTracking()
            .Where(h => h.WantedId == wantedId)
            .OrderByDescending(h => h.Priority).ThenByDescending(h => h.CreatedAt)
            .Take(ListCap)
            .Select(h => new { h.Id, h.CaseNumber, h.Status, h.CreatedAt, h.Text, h.Priority })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TipNoticeRow(r.Id, r.CaseNumber, r.Status, r.CreatedAt, Excerpt(r.Text), r.Priority))
            .ToList();
    }

    public async Task<IReadOnlyList<TipHistoryRow>> GetForLinkedPersonAsync(string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profileIds = await db.BuergerProfile.AsNoTracking()
            .Where(p => p.LinkedPersonId == personId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        if (profileIds.Count == 0)
        {
            return [];
        }

        // TipAnonymity.Disclosable, not a hand-written clause: this surface is keyed on the citizen, so a promised
        // tip must not show up here in any form
        var rows = await db.Hinweise.AsNoTracking()
            .Where(TipAnonymity.Disclosable)
            .Where(h => profileIds.Contains(h.CitizenProfileId))
            .OrderByDescending(h => h.CreatedAt)
            .Take(ListCap)
            .Select(h => new
            {
                h.Id,
                h.CaseNumber,
                h.Status,
                h.CreatedAt,
                h.Text,
                CitizenFirstName = h.CitizenProfile!.FirstName,
                CitizenLastName = h.CitizenProfile!.LastName,
                ConfirmedTips = h.CitizenProfile!.ConfirmedTips,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TipHistoryRow(r.Id, r.CaseNumber, r.Status, r.CreatedAt, Excerpt(r.Text),
                Name(r.CitizenFirstName, r.CitizenLastName), TipTrust.Tier(r.ConfirmedTips)))
            .ToList();
    }

    public async Task<IReadOnlyList<TipDuplicateRow>> GetDuplicatesAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.Hinweise.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => h.DuplicateGroupId)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(group))
        {
            return [];
        }

        var rows = await db.Hinweise.AsNoTracking()
            .Where(h => h.DuplicateGroupId == group && h.Id != id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(ListCap)
            .Select(h => new { h.Id, h.CaseNumber, h.Status, h.CreatedAt, h.Text })
            .ToListAsync(cancellationToken);
        return rows
            .Select(r => new TipDuplicateRow(r.Id, r.CaseNumber, r.Status, r.CreatedAt, Excerpt(r.Text)))
            .ToList();
    }

    public async Task<IReadOnlyList<TipMessageRow>> GetMessagesAsync(string id, TipMessageAudience audience,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.HinweisNachrichten.AsNoTracking()
            .Where(m => m.HinweisId == id && m.Audience == audience)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TipMessageRow(m.Id, m.Audience, m.Text, m.AuthorIsCitizen,
                m.AuthorAgent!.Codename, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AssignSelfAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        row.HandlerId = actor.GetAgentId();
        if (row.Status == TipStatus.Neu)
        {
            row.Status = TipStatus.InPruefung;
        }
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id);
    }

    public async Task SetStatusAsync(string id, TipStatus status, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        if (!TipRules.IsTransitionAllowed(row.Status, status))
        {
            throw new InvalidOperationException(
                $"Der Wechsel von „{TipStatusDisplay.Name(row.Status)}“ nach „{TipStatusDisplay.Name(status)}“ "
                + "ist nicht vorgesehen.");
        }
        var wasConfirmed = TipRules.CountsAsConfirmed(row.Status);
        row.Status = status;
        row.HandlerId ??= actor.GetAgentId();
        await db.SaveChangesAsync(cancellationToken);

        // the trust tier moved, so the citizen's whole open queue is re-ordered
        if (wasConfirmed || TipRules.CountsAsConfirmed(status))
        {
            await buerger.RecomputeConfirmedTipsAsync(row.CitizenProfileId, cancellationToken);
            await priority.StampForCitizenAsync(row.CitizenProfileId, cancellationToken);
        }
        broadcaster.Report(row.Id);
    }

    public async Task PostInternalNoteAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        db.HinweisNachrichten.Add(new HinweisNachricht
        {
            HinweisId = row.Id,
            Audience = TipMessageAudience.Intern,
            Text = body,
            AuthorAgentId = actor.GetAgentId(),
        });
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(row.Id);
    }

    public async Task AskCitizenAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var body = CleanMessage(text);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);
        if (TipRules.IsClosed(row.Status))
        {
            throw new InvalidOperationException("Dieser Hinweis ist abgeschlossen.");
        }

        db.HinweisNachrichten.Add(new HinweisNachricht
        {
            HinweisId = row.Id,
            Audience = TipMessageAudience.Buerger,
            Text = body,
            // no author: the citizen-facing row structurally carries no agent, so the outward view has nothing to hide
            AuthorAgentId = null,
        });
        row.HandlerId ??= actor.GetAgentId();
        if (row.Status is TipStatus.Neu or TipStatus.InPruefung)
        {
            row.Status = TipStatus.Rueckfrage;
        }
        await db.SaveChangesAsync(cancellationToken);

        await NotifyCitizenAsync(db, row, cancellationToken);
        broadcaster.Report(row.Id);
    }

    public async Task ResolveAnonymityAsync(string id, string reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // RequireTipHandling first, not RequireWriteAccess: that guard deliberately lets the demo principal
        // through and relies on the ReadOnlyBarrierInterceptor, which an ExecuteUpdate below never reaches.
        // MayWrite() inside RequireTipHandling denies demo, supervision, partners and citizens.
        Permission.RequireTipHandling(actor);
        Permission.RequireLeadership(actor);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Eine Auflösung braucht eine Begründung.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Hinweise.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => new { h.Id, h.WantsAnonymity, h.AnonymityResolvedAt })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Hinweis nicht gefunden.");
        if (!row.WantsAnonymity)
        {
            throw new InvalidOperationException("Dieser Hinweis wurde nicht anonym abgegeben.");
        }
        if (row.AnonymityResolvedAt is not null)
        {
            throw new InvalidOperationException("Die Anonymität ist bereits aufgelöst.");
        }

        // ExecuteUpdate rather than a tracked write: the interceptor would log the two timestamps and lose the
        // reason, which is the only part of this that matters later
        await db.Hinweise.Where(h => h.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(h => h.AnonymityResolvedAt, DateTime.UtcNow)
            .SetProperty(h => h.AnonymityResolvedById, actor.GetAgentId()), cancellationToken);

        var changes = ManualAudit.Change("Anonymität", "gewahrt", "aufgelöst");
        changes["Begründung"] = [null, reason.Trim()];
        db.AuditLogs.Add(ManualAudit.Row(nameof(Hinweis), id, AuditAction.Modified, actor, changes));
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await GetOrThrowAsync(db, id, cancellationToken);

        // money history is append-only: the reward rows reach their tip through a required navigation, so a
        // soft-deleted tip would hide the citizen's own receipt and the payout row along with it
        if (await db.HinweisBelohnungen.AnyAsync(b => b.TipId == id, cancellationToken))
        {
            throw new InvalidOperationException("Ein belohnter Hinweis lässt sich nicht löschen — eine "
                + "Fehlbuchung wird in der Kasse gegengebucht.");
        }

        // the interceptor rewrites this into a soft delete; the attachment stays until the tip is purged for good
        db.Hinweise.Remove(row);
        await db.SaveChangesAsync(cancellationToken);

        // a deleted tip stops counting towards the trust tier, so the counter is recomputed rather than left standing
        if (TipRules.CountsAsConfirmed(row.Status))
        {
            await buerger.RecomputeConfirmedTipsAsync(row.CitizenProfileId, cancellationToken);
            await priority.StampForCitizenAsync(row.CitizenProfileId, cancellationToken);
        }
        broadcaster.Report(id);
    }

    public async Task<TipAttachmentAccess?> GetAttachmentAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Hinweise.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => new { h.CitizenProfileId, h.AttachmentFileName, h.AttachmentContentType, h.AttachmentOriginalName })
            .FirstOrDefaultAsync(cancellationToken);
        if (row?.AttachmentFileName is null)
        {
            return null;
        }

        if (!actor.IsInternalAgent())
        {
            // not an agent: the owner and nobody else
            var profile = await buerger.GetOwnAsync(actor, cancellationToken);
            if (profile is null || profile.Id != row.CitizenProfileId)
            {
                return null;
            }
        }

        return new TipAttachmentAccess(row.AttachmentFileName, row.AttachmentContentType, row.AttachmentOriginalName);
    }

    // ---- reward ----

    public async Task<TipRewardTarget> MarkRewardedAsync(AppDbContext db, string tipId, decimal amount,
        string receiptNumber, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var row = await GetOrThrowAsync(db, tipId, cancellationToken);
        if (!TipRules.IsTransitionAllowed(row.Status, TipStatus.FuehrteZurErgreifung))
        {
            throw new InvalidOperationException(
                $"Ein Hinweis im Status „{TipStatusDisplay.Name(row.Status)}“ lässt sich nicht als belohnt schließen.");
        }

        row.Status = TipStatus.FuehrteZurErgreifung;
        row.HandlerId ??= actor.GetAgentId();
        // written here rather than through AskCitizenAsync: that one refuses a closed tip and would switch the
        // status to Rueckfrage. No author, so the line reads as "NOOSE" outside.
        db.HinweisNachrichten.Add(new HinweisNachricht
        {
            HinweisId = row.Id,
            Audience = TipMessageAudience.Buerger,
            Text = $"Ihr Hinweis hat zur Ergreifung geführt. Belohnung: {Money.Format(amount)} · Beleg {receiptNumber}.",
            AuthorAgentId = null,
        });

        await db.SaveChangesAsync(cancellationToken);
        return new TipRewardTarget(row.Id, row.CaseNumber, row.CitizenProfileId);
    }

    public async Task AfterRewardAsync(IReadOnlyList<TipRewardTarget> targets,
        CancellationToken cancellationToken = default)
    {
        foreach (var target in targets)
        {
            // the trust tier moved, so the citizen's whole open queue is re-ordered
            await buerger.RecomputeConfirmedTipsAsync(target.CitizenProfileId, cancellationToken);
            await priority.StampForCitizenAsync(target.CitizenProfileId, cancellationToken);
            await NotifyRewardAsync(target, cancellationToken);
            broadcaster.Report(target.TipId);
        }
    }

    // ---- trash ----

    public async Task<List<Hinweis>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Hinweise.IgnoreQueryFilters().AsNoTracking()
            .Where(h => h.IsDeleted)
            .OrderByDescending(h => h.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // scoped to the bin like every other restore in this layer: without it a live row can be pushed through the
        // restore path, saved and broadcast as a no-op
        var row = await db.Hinweise.IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == id && h.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Der Hinweis liegt nicht im Papierkorb.");
        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);

        if (TipRules.CountsAsConfirmed(row.Status))
        {
            await buerger.RecomputeConfirmedTipsAsync(row.CitizenProfileId, cancellationToken);
            await priority.StampForCitizenAsync(row.CitizenProfileId, cancellationToken);
        }
        broadcaster.Report(id);
    }

    // ---- helpers ----

    // the reference is verified through the published read path, never taken as an id: accepting a raw id would
    // turn the form into an existence oracle for drafts and retracted notices
    private async Task<string?> ResolveNoticeAsync(AppDbContext db, string? caseNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
        {
            return null;
        }
        var notice = await wanted.GetByCaseNumberAsync(caseNumber, ct);
        if (notice is null)
        {
            throw new InvalidOperationException("Zu diesem Aktenzeichen gibt es keine laufende Ausschreibung.");
        }
        return await db.OeffentlicheFahndungen.AsNoTracking()
            .Where(f => f.CaseNumber == notice.CaseNumber)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Hinweis> GetOrThrowAsync(AppDbContext db, string id, CancellationToken ct)
        => await db.Hinweise.FirstOrDefaultAsync(h => h.Id == id, ct)
           ?? throw new InvalidOperationException("Hinweis nicht gefunden.");

    private static System.Linq.Expressions.Expression<Func<Hinweis, bool>> ScopeFilter(TipInboxScope scope)
        => scope switch
        {
            TipInboxScope.Eingang => h => h.Status == TipStatus.Neu,
            TipInboxScope.Bearbeitung => h => h.Status == TipStatus.InPruefung || h.Status == TipStatus.Rueckfrage,
            _ => h => h.Status == TipStatus.Bestaetigt || h.Status == TipStatus.Verworfen
                      || h.Status == TipStatus.FuehrteZurErgreifung,
        };

    // one line per group, so a page of 200 rows costs one query instead of one per group
    private static async Task<Dictionary<string, int>> DuplicateCountsAsync(AppDbContext db,
        List<string> groupIds, CancellationToken ct)
    {
        if (groupIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
        var rows = await db.Hinweise.AsNoTracking()
            .Where(h => h.DuplicateGroupId != null && groupIds.Contains(h.DuplicateGroupId))
            .GroupBy(h => h.DuplicateGroupId!)
            .Select(g => new { Group = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Group, r => r.Count, StringComparer.Ordinal);
    }

    // grouping runs after the commit and never fails the submission: the tip is stored, the group is convenience
    private async Task GroupDuplicatesAsync(AppDbContext db, Hinweis row, CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-TipDuplicates.CandidateDays);
            var query = db.Hinweise.AsNoTracking().Where(h => h.Id != row.Id && h.CreatedAt >= since);
            // spelled out: comparing a column to a null variable translates to SQL NULL and would find nothing
            query = row.WantedId is null
                ? query.Where(h => h.WantedId == null)
                : query.Where(h => h.WantedId == row.WantedId);

            var candidates = await query
                .OrderByDescending(h => h.CreatedAt)
                .Take(TipDuplicates.CandidateCap)
                .Select(h => new { h.Id, h.Text, h.DuplicateGroupId })
                .ToListAsync(ct);
            if (candidates.Count == 0)
            {
                return;
            }

            var words = TextSimilarity.Tokens(row.Text);
            var match = candidates
                .FirstOrDefault(c => TipDuplicates.AreDuplicates(words, TextSimilarity.Tokens(c.Text)));
            if (match is null)
            {
                return;
            }

            var group = match.DuplicateGroupId ?? Guid.NewGuid().ToString();
            if (match.DuplicateGroupId is null)
            {
                await db.Hinweise.Where(h => h.Id == match.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(h => h.DuplicateGroupId, group), ct);
            }
            await db.Hinweise.Where(h => h.Id == row.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.DuplicateGroupId, group), ct);
            // the tracked entity is left alone on purpose: assigning the group would mark it Modified, and the next
            // SaveChanges on this context would stamp GeaendertAm and write the audit row this path avoids
        }
        catch (Exception)
        {
            /* best effort */
        }
    }

    private static bool IsHidden(bool wantsAnonymity, DateTime? resolvedAt)
        => TipAnonymity.IsHidden(wantsAnonymity, resolvedAt);

    private static string? Name(string? first, string? last)
    {
        var name = $"{first} {last}".Trim();
        return name.Length == 0 ? null : name;
    }

    private static string Excerpt(string text)
        => text.Length <= ExcerptLength ? text : text[..ExcerptLength] + "…";

    private static string? Trim(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];

    private static string CleanMessage(string? text)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            throw new InvalidOperationException("Bitte gib einen Text ein.");
        }
        if (body.Length > TipRules.MaxMessageLength)
        {
            throw new InvalidOperationException(
                $"Eine Nachricht fasst höchstens {TipRules.MaxMessageLength} Zeichen.");
        }
        return body;
    }

    // agency messages per tip, so the unread count can be computed against each tip's own read mark
    private static async Task<Dictionary<string, List<DateTime>>> UnreadByTipAsync(
        AppDbContext db, List<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, List<DateTime>>();
        }
        var rows = await db.HinweisNachrichten.AsNoTracking()
            .Where(m => ids.Contains(m.HinweisId) && m.Audience == TipMessageAudience.Buerger && !m.AuthorIsCitizen)
            .Select(m => new { m.HinweisId, m.CreatedAt })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.HinweisId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.CreatedAt).ToList());
    }

    private static int UnreadFor(Dictionary<string, List<DateTime>> map, string id, DateTime? readAt)
        => map.TryGetValue(id, out var stamps)
            ? stamps.Count(s => readAt is null || s > readAt)
            : 0;

    // computed in memory: "the newest row of this thread" per tip is one window function EF would have to invent,
    // and the list is capped at ListCap anyway
    private static async Task<Dictionary<string, (DateTime At, bool FromCitizen)>> LastCitizenMessageAsync(
        AppDbContext db, List<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, (DateTime, bool)>();
        }
        var rows = await db.HinweisNachrichten.AsNoTracking()
            .Where(m => ids.Contains(m.HinweisId) && m.Audience == TipMessageAudience.Buerger)
            .Select(m => new { m.HinweisId, m.CreatedAt, m.AuthorIsCitizen })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.HinweisId)
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

    // leadership hears about a new tip; everyone else has the inbox badge. A bell for every agent on every
    // submission would train the whole office to ignore the bell
    private async Task NotifyDeskAsync(AppDbContext db, Hinweis row, CancellationToken ct)
    {
        try
        {
            var recipients = await DeskRecipientsAsync(db, ct);
            await notifications.NotifyManyAsync(recipients, NotificationType.PublicTipReceived,
                $"Neuer Bürgerhinweis {row.CaseNumber}", $"/hinweise/{row.Id}", null, ct);
        }
        catch
        {
            /* best effort */
        }
    }

    private async Task NotifyHandlerAsync(AppDbContext db, Hinweis row, CancellationToken ct)
    {
        try
        {
            var recipients = row.HandlerId is { Length: > 0 } handler
                ? new List<string> { handler }
                : await DeskRecipientsAsync(db, ct);
            await notifications.NotifyManyAsync(recipients, NotificationType.PublicTipReceived,
                $"Antwort zum Hinweis {row.CaseNumber}", $"/hinweise/{row.Id}", null, ct);
        }
        catch
        {
            /* best effort */
        }
    }

    private async Task NotifyCitizenAsync(AppDbContext db, Hinweis row, CancellationToken ct)
    {
        try
        {
            var userId = await db.BuergerProfile.AsNoTracking()
                .Where(p => p.Id == row.CitizenProfileId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(ct);
            await notifications.NotifyAsync(userId, NotificationType.PublicTipAnswered,
                $"Rückfrage zu deinem Hinweis {row.CaseNumber}", $"/buerger/hinweise/{row.CaseNumber}", ct);
        }
        catch
        {
            /* best effort */
        }
    }

    private async Task NotifyRewardAsync(TipRewardTarget target, CancellationToken ct)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var userId = await db.BuergerProfile.AsNoTracking()
                .Where(p => p.Id == target.CitizenProfileId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync(ct);
            await notifications.NotifyAsync(userId, NotificationType.PublicRewardPaid,
                $"Belohnung für deinen Hinweis {target.CaseNumber}",
                $"/buerger/hinweise/{target.CaseNumber}", ct);
        }
        catch
        {
            /* best effort */
        }
    }

    private static Task<List<string>> DeskRecipientsAsync(AppDbContext db, CancellationToken ct)
        => db.Users.OnlySelectable()
            .Where(u => u.IsAdmin || u.Rank >= Rank.SupervisorySpecialAgent)
            .Select(u => u.Id)
            .ToListAsync(ct);
}
