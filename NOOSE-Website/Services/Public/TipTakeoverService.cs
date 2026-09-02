using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Cases;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="ITipTakeoverService" />
public class TipTakeoverService(
    IDbContextFactory<AppDbContext> dbFactory,
    ITipService tips,
    IPersonService people,
    ICaseService cases,
    IObservationService observations,
    ILinkService links) : ITipTakeoverService
{
    private const string NotFound = "Hinweis nicht gefunden.";

    public async Task<IReadOnlyList<LinkDisplay>> GetStateAsync(string tipId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipRead(actor);
        return await links.GetForRecordAsync(nameof(Hinweis), tipId, ViewerScope.From(actor),
            cancellationToken: cancellationToken);
    }

    public async Task<string> ToNewPersonAsync(string tipId, string name, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var tip = await TipAsync(tipId, cancellationToken);
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            throw new InvalidOperationException("Bitte einen Namen für die neue Personenakte angeben.");
        }
        await RequireNoPersonYetAsync(tipId, cancellationToken);

        // no description: citizen prose does not belong in the file's own text, and the tip is one link away
        var person = await people.CreateAsync(new PersonInput { Name = clean }, actor, cancellationToken);
        try
        {
            await LinkAsync(tip, nameof(Person), person.Id, actor, cancellationToken);
        }
        catch (Exception)
        {
            // lost a race with a second tab: discard this duplicate file rather than leave two for one tip.
            // Same rollback as ApplicationCaseService — a soft delete plus the row that says why.
            await DiscardPersonAsync(person.Id, actor, cancellationToken);
            throw;
        }

        // The pre-check above is only a read, so two tabs can both pass it: Links carries no unique index for this
        // pair (a source may legitimately link to many targets of one type), which is why the documented catch
        // never fired. The loser is therefore detected AFTER the link and discards the file it just created; the
        // oldest link wins, so both tabs agree on which one survives.
        var survivor = await OldestLinkedPersonAsync(tipId, cancellationToken);
        if (survivor is not null && !string.Equals(survivor, person.Id, StringComparison.Ordinal))
        {
            await DiscardPersonAsync(person.Id, actor, cancellationToken);
            throw new InvalidOperationException(
                "Dieser Hinweis wurde soeben in eine andere Personenakte übernommen. Öffne sie über die Verknüpfung.");
        }

        await AdvanceAsync(tip, actor, cancellationToken);
        return person.Id;
    }

    /// <summary>The person of the oldest Hinweis-to-Person link, so concurrent takeovers pick the same winner.</summary>
    private async Task<string?> OldestLinkedPersonAsync(string tipId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Links.AsNoTracking()
            .Where(v => v.SourceType == nameof(Hinweis) && v.SourceId == tipId && v.TargetType == nameof(Person))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => v.TargetId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AttachPersonAsync(string tipId, string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var tip = await TipAsync(tipId, cancellationToken);

        // the visibility of the target is LinkService's job: a classified file is refused there
        await LinkAsync(tip, nameof(Person), personId, actor, cancellationToken);
        await AdvanceAsync(tip, actor, cancellationToken);
    }

    public async Task<string> ToCaseAsync(string tipId, string? title, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var tip = await TipAsync(tipId, cancellationToken);

        var @case = await cases.CreateAsync(new CaseInput
        {
            Title = string.IsNullOrWhiteSpace(title) ? $"Bürgerhinweis {tip.CaseNumber}" : title.Trim(),
            Status = CaseStatus.Open,
            Classification = Classification.Unknown,
            SecrecyLevel = DocumentClassification.None,
        }, actor, cancellationToken);

        try
        {
            await LinkAsync(tip, nameof(Case), @case.Id, actor, cancellationToken);
        }
        catch (Exception)
        {
            await DiscardCaseAsync(@case.Id, actor, cancellationToken);
            throw;
        }

        await AdvanceAsync(tip, actor, cancellationToken);
        return @case.Id;
    }

    public async Task<string> ToObservationAsync(string tipId, string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireTipHandling(actor);
        var tip = await TipAsync(tipId, cancellationToken);
        // ObservationService only gates the file's secrecy level, not its classification, and the person id arrives
        // from the client — without this an agent could write into a file they may not open
        await RequireVisiblePersonAsync(personId, actor, cancellationToken);

        // the sighting is the tip, and the observer was a citizen, not an agent
        var observation = await observations.CreateAsync(personId, new ObservationInput
        {
            Start = tip.CreatedAt,
            Sighting = tip.Text,
            ObservingAgentId = null,
        }, actor, cancellationToken);

        await LinkAsync(tip, nameof(Observation), observation.Id, actor, cancellationToken);
        await AdvanceAsync(tip, actor, cancellationToken);
        return observation.Id;
    }

    // ---- helpers ----

    private sealed record TipRef(string Id, string CaseNumber, string Text, DateTime CreatedAt, TipStatus Status);

    private async Task<TipRef> TipAsync(string tipId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Hinweise.AsNoTracking()
            .Where(h => h.Id == tipId)
            .Select(h => new TipRef(h.Id, h.CaseNumber, h.Text, h.CreatedAt, h.Status))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
    }

    /// <summary>A manual link, never an automatic one: the timeline and the chronicle skip automatic links.</summary>
    private Task LinkAsync(TipRef tip, string targetType, string targetId, ClaimsPrincipal actor,
        CancellationToken cancellationToken)
        => links.CreateAsync(nameof(Hinweis), tip.Id, targetType, targetId,
            $"Übernahme aus Bürgerhinweis {tip.CaseNumber}", actor, LinkKind.Default, cancellationToken);

    /// <summary>A fresh tip moves into review, exactly as claiming it does; confirming stays a separate decision.</summary>
    private async Task AdvanceAsync(TipRef tip, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (tip.Status == TipStatus.Neu)
        {
            await tips.SetStatusAsync(tip.Id, TipStatus.InPruefung, actor, cancellationToken);
        }
    }

    /// <summary>The record read gate for a person id that came from outside; AttachPersonAsync gets it from LinkService.</summary>
    private async Task RequireVisiblePersonAsync(string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId, ViewerScope.From(actor),
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Auf diese Akte darfst du nicht schreiben (Verschlusssache oder nicht vorhanden).");
        }
    }

    private async Task RequireNoPersonYetAsync(string tipId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.Links.AnyAsync(v => v.SourceType == nameof(Hinweis) && v.SourceId == tipId
                                                 && v.TargetType == nameof(Person), cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException(
                "Dieser Hinweis ist bereits in eine Personenakte übernommen. Öffne sie über die Verknüpfung.");
        }
    }

    private async Task DiscardPersonAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.People.Where(p => p.Id == personId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAt, DateTime.UtcNow)
                .SetProperty(p => p.DeletedById, actor.GetAgentId()), cancellationToken);
        db.AuditLogs.Add(ManualAudit.Row(nameof(Person), personId, AuditAction.Deleted, actor,
            ManualAudit.Change("Verworfen", null, "Doppelte Übernahme eines Bürgerhinweises")));
        await db.SaveChangesAsync(cancellationToken);
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
            ManualAudit.Change("Verworfen", null, "Doppelte Übernahme eines Bürgerhinweises")));
        await db.SaveChangesAsync(cancellationToken);
    }
}
