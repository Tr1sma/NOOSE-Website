using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBewerbungService" />
public class BewerbungService(
    IDbContextFactory<AppDbContext> dbFactory,
    ICaseNumberService caseNumbers,
    ISourcesStorageService storage,
    BewerbungBroadcaster broadcaster,
    INotificationService notifications) : IBewerbungService
{
    public async Task<Bewerbung?> GetOwnAsync(ClaimsPrincipal applicant, CancellationToken cancellationToken = default)
    {
        var userId = applicant.GetAgentId();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bewerbungen.AsNoTracking()
            .Where(b => b.ApplicantUserId == userId)
            .OrderByDescending(b => b.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Bewerbung> SubmitAsync(BewerbungSubmitModel model, Stream? attachment, string? originalName,
        string? contentType, ClaimsPrincipal applicant, CancellationToken cancellationToken = default)
    {
        Permission.RequireApplicant(applicant);
        var userId = applicant.GetAgentId()
            ?? throw new InvalidOperationException("Kein Benutzerkontext.");

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            throw new InvalidOperationException("Bitte gib deinen Namen an.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Bewerbungen.AnyAsync(b => b.ApplicantUserId == userId, cancellationToken))
        {
            throw new InvalidOperationException("Es liegt bereits eine Bewerbung für dieses Konto vor.");
        }

        string? fileNameSaved = null;
        if (attachment is not null && !string.IsNullOrWhiteSpace(originalName))
        {
            if (!string.IsNullOrWhiteSpace(contentType) && !storage.IsAllowedType(contentType))
            {
                throw new InvalidOperationException("Dieser Dateityp ist nicht erlaubt.");
            }
            fileNameSaved = await storage.SaveAsync(attachment, originalName!, cancellationToken);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var caseNumber = await caseNumbers.NextAsync(db, "B", cancellationToken);

        var bewerbung = new Bewerbung
        {
            CaseNumber = caseNumber,
            ApplicantUserId = userId,
            AcademicDegree = Trim(model.AcademicDegree),
            Name = model.Name.Trim(),
            BirthDate = model.BirthDate,
            Employer = Trim(model.Employer),
            PriorExperience = Trim(model.PriorExperience),
            CoverLetter = Trim(model.CoverLetter),
            AttachmentFileNameSaved = fileNameSaved,
            AttachmentOriginalName = Trim(originalName),
            AttachmentContentType = Trim(contentType),
            Status = BewerbungStatus.Eingereicht,
            SubmittedAt = DateTime.UtcNow,
        };
        db.Bewerbungen.Add(bewerbung);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        try
        {
            var recipients = await HrbRecipientIdsAsync(db, cancellationToken);
            await notifications.NotifyManyAsync(recipients, NotificationType.Recruiting,
                $"Neue Bewerbung ({bewerbung.CaseNumber})", $"/bewerbungen/{bewerbung.Id}", userId, cancellationToken);
        }
        catch { /* best effort */ }

        return bewerbung;
    }

    public async Task<List<Bewerbung>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bewerbungen.AsNoTracking()
            .OrderByDescending(b => b.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Bewerbung?> GetForHrbAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Bewerbungen.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Bewerbung?> GetForFileAccessAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await db.Bewerbungen.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bewerbung is null)
        {
            return null;
        }
        var isOwner = bewerbung.ApplicantUserId == actor.GetAgentId();
        return isOwner || actor.IsHrbOrLeadership() ? bewerbung : null;
    }

    public async Task AssignSelfAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await GetOrThrow(db, id, cancellationToken);
        bewerbung.AssignedAgentId = actor.GetAgentId();
        bewerbung.AssignedAgentName = actor.GetCodename();
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);
    }

    public async Task SetStatusAsync(string id, BewerbungStatus target, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await GetOrThrow(db, id, cancellationToken);

        if (BewerbungStatusDisplay.IsTerminal(bewerbung.Status))
        {
            throw new InvalidOperationException("Diese Bewerbung ist bereits abgeschlossen.");
        }
        if (!IsTransitionAllowed(bewerbung.Status, target))
        {
            throw new InvalidOperationException(
                $"Wechsel von „{BewerbungStatusDisplay.Name(bewerbung.Status)}“ zu „{BewerbungStatusDisplay.Name(target)}“ ist nicht möglich.");
        }

        bewerbung.Status = target;
        if (BewerbungStatusDisplay.IsTerminal(target))
        {
            bewerbung.DecidedByName = actor.GetCodename();
            bewerbung.DecidedAt = DateTime.UtcNow;
            bewerbung.DecisionNote = Trim(note);
        }
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);

        try
        {
            await notifications.NotifyAsync(bewerbung.ApplicantUserId, NotificationType.Recruiting,
                $"Statusänderung: {BewerbungStatusDisplay.Name(target)}", "/portal/status", cancellationToken);
        }
        catch { /* best effort */ }
    }

    public async Task SetSecurityResultAsync(string id, bool passed, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await GetOrThrow(db, id, cancellationToken);
        if (BewerbungStatusDisplay.IsTerminal(bewerbung.Status))
        {
            throw new InvalidOperationException("Diese Bewerbung ist bereits abgeschlossen.");
        }

        bewerbung.SecurityCheckPassed = passed;
        if (!passed)
        {
            bewerbung.Status = BewerbungStatus.Abgelehnt;
            bewerbung.DecidedByName = actor.GetCodename();
            bewerbung.DecidedAt = DateTime.UtcNow;
            bewerbung.DecisionNote = "Sicherheitsüberprüfung nicht bestanden.";
        }
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);

        if (!passed)
        {
            try
            {
                await notifications.NotifyAsync(bewerbung.ApplicantUserId, NotificationType.Recruiting,
                    "Deine Bewerbung wurde abgeschlossen.", "/portal/status", cancellationToken);
            }
            catch { /* best effort */ }
        }
    }

    public async Task LinkPersonAsync(string id, string? personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await GetOrThrow(db, id, cancellationToken);

        if (string.IsNullOrWhiteSpace(personId))
        {
            bewerbung.LinkedPersonId = null;
        }
        else
        {
            if (!await db.People.AnyAsync(p => p.Id == personId, cancellationToken))
            {
                throw new InvalidOperationException("Die ausgewählte Person wurde nicht gefunden.");
            }
            bewerbung.LinkedPersonId = personId;
        }
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);
    }

    public async Task<LinkedPersonInfo?> GetLinkedPersonAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var personId = await db.Bewerbungen.AsNoTracking()
            .Where(b => b.Id == id).Select(b => b.LinkedPersonId).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(personId))
        {
            return null;
        }
        var person = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null)
        {
            return null;
        }
        // hide a classified person from HRB members without classified-read
        if (person.IsClassified && !actor.MayClassifiedRead())
        {
            return null;
        }
        return new LinkedPersonInfo(person.Id, person.Name, person.CaseNumber,
            person.ThreatScore, person.ThreatConfidence, person.ScoreCalculatedAt, person.IsClassified);
    }

    public async Task<List<BewerbungMessage>> GetMessagesAsync(string id, BewerbungMessageAudience audience, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCanReadAsync(db, id, audience, actor, cancellationToken);
        return await db.BewerbungMessages.AsNoTracking()
            .Where(m => m.BewerbungId == id && m.Audience == audience)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<BewerbungMessage> PostInternalAsync(string id, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        var content = (text ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        _ = await GetOrThrow(db, id, cancellationToken);

        var message = new BewerbungMessage
        {
            BewerbungId = id,
            Audience = BewerbungMessageAudience.Intern,
            Text = content,
            AuthorName = actor.GetCodename(),
        };
        db.BewerbungMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);

        try
        {
            var who = string.IsNullOrWhiteSpace(actor.GetCodename()) ? "Ein Agent" : actor.GetCodename();
            await notifications.NotifyMentionedAsync(content, $"{who} hat dich in einer Bewerbung erwähnt.",
                $"/bewerbungen/{id}", nameof(Bewerbung), id, actor, cancellationToken);
        }
        catch { /* best effort */ }

        return message;
    }

    public async Task<BewerbungMessage> PostToApplicantAsync(string id, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        var content = (text ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await GetOrThrow(db, id, cancellationToken);

        // sanitize the rich-text HTML, then redact the sender's name token (the applicant is addressed by name)
        content = HtmlCleanup.Clean(content);
        content = BewerbungTemplateRenderer.Redact(content, bewerbung.Name);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        var message = new BewerbungMessage
        {
            BewerbungId = id,
            Audience = BewerbungMessageAudience.Bewerber,
            Text = content,
            AuthorName = actor.GetCodename(),
            AuthorIsApplicant = false,
        };
        db.BewerbungMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);

        try
        {
            await notifications.NotifyAsync(bewerbung.ApplicantUserId, NotificationType.Recruiting,
                "Neue Nachricht zu deiner Bewerbung", "/portal/status", cancellationToken);
        }
        catch { /* best effort */ }

        return message;
    }

    public async Task<BewerbungMessage> PostAsApplicantAsync(string id, string text, ClaimsPrincipal applicant, CancellationToken cancellationToken = default)
    {
        Permission.RequireApplicant(applicant);
        var content = (text ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bewerbung = await GetOrThrow(db, id, cancellationToken);
        if (bewerbung.ApplicantUserId != applicant.GetAgentId())
        {
            throw new UnauthorizedAccessException("Das ist nicht deine Bewerbung.");
        }

        // sanitize the applicant's rich-text HTML before it is rendered to HRB
        content = HtmlCleanup.Clean(content);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        var message = new BewerbungMessage
        {
            BewerbungId = id,
            Audience = BewerbungMessageAudience.Bewerber,
            Text = content,
            AuthorIsApplicant = true,
        };
        db.BewerbungMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(id);

        try
        {
            var recipient = bewerbung.AssignedAgentId;
            if (!string.IsNullOrEmpty(recipient))
            {
                await notifications.NotifyAsync(recipient, NotificationType.Recruiting,
                    $"Bewerber-Antwort ({bewerbung.CaseNumber})", $"/bewerbungen/{id}", cancellationToken);
            }
            else
            {
                var recipients = await HrbRecipientIdsAsync(db, cancellationToken);
                await notifications.NotifyManyAsync(recipients, NotificationType.Recruiting,
                    $"Bewerber-Antwort ({bewerbung.CaseNumber})", $"/bewerbungen/{id}", applicant.GetAgentId(), cancellationToken);
            }
        }
        catch { /* best effort */ }

        return message;
    }

    private async Task EnsureCanReadAsync(AppDbContext db, string id, BewerbungMessageAudience audience, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (audience == BewerbungMessageAudience.Intern)
        {
            Permission.RequireHrbOrLeadership(actor);
            return;
        }
        // Bewerber audience: owner or HRB/leadership
        if (actor.IsHrbOrLeadership())
        {
            return;
        }
        var ownerId = await db.Bewerbungen.AsNoTracking()
            .Where(b => b.Id == id).Select(b => b.ApplicantUserId).FirstOrDefaultAsync(cancellationToken);
        if (ownerId is null || ownerId != actor.GetAgentId())
        {
            throw new UnauthorizedAccessException("Diese Konversation ist für dich nicht zugänglich.");
        }
    }

    private static async Task<Bewerbung> GetOrThrow(AppDbContext db, string id, CancellationToken cancellationToken)
        => await db.Bewerbungen.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
           ?? throw new InvalidOperationException("Bewerbung nicht gefunden.");

    private static Task<List<string>> HrbRecipientIdsAsync(AppDbContext db, CancellationToken cancellationToken)
        => db.Users
            .Where(u => u.Status == AgentStatus.Active && (u.IsHRB || u.IsAdmin || u.Rank >= Rank.SupervisorySpecialAgent))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    private static bool IsTransitionAllowed(BewerbungStatus current, BewerbungStatus target)
    {
        // closing or rejecting is allowed from any non-terminal state
        if (target is BewerbungStatus.Abgelehnt or BewerbungStatus.Geschlossen)
        {
            return true;
        }
        return (current, target) switch
        {
            (BewerbungStatus.Eingereicht, BewerbungStatus.InSicherheitspruefung) => true,
            (BewerbungStatus.InSicherheitspruefung, BewerbungStatus.ImTest) => true,
            (BewerbungStatus.InSicherheitspruefung, BewerbungStatus.ImVorstellungsgespraech) => true,
            (BewerbungStatus.ImTest, BewerbungStatus.ImVorstellungsgespraech) => true,
            (BewerbungStatus.ImVorstellungsgespraech, BewerbungStatus.Angenommen) => true,
            _ => false,
        };
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
