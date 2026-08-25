using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IBuergerService" />
/// <remarks>
/// No <see cref="ManualAudit"/> anywhere: <see cref="BuergerProfil"/> is <c>IAuditable</c> and every write here
/// goes through a plain SaveChanges, so the audit interceptor already records the field diff. A manual row would
/// double-log the same rename. The one exception is the trust counter, which is a derived cache and deliberately
/// silent — like a threat score, it would otherwise stamp the profile on every status change of any tip.
/// </remarks>
public class BuergerService(IDbContextFactory<AppDbContext> dbFactory) : IBuergerService
{
    private const int NameMaxLength = 64;

    public async Task<BuergerProfil?> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireCitizenPortal(actor);

        var userId = actor.GetAgentId();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BuergerProfile.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<BuergerProfil> SaveOwnAsync(string firstName, string lastName, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireCitizenPortal(actor);
        // a civilian identity is a write like any other: read-only supervision and partners stay read-only here too
        Permission.RequireWriteAccess(actor);

        var userId = actor.GetAgentId()
            ?? throw new InvalidOperationException("Kein angemeldetes Konto.");
        var first = Clean(firstName, "Vorname");
        var last = Clean(lastName, "Nachname");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.BuergerProfile.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new BuergerProfil { UserId = userId, FirstName = first, LastName = last };
            db.BuergerProfile.Add(profile);
        }
        else
        {
            // a blocked citizen may still correct their own name; the block governs submissions, not identity
            profile.FirstName = first;
            profile.LastName = last;
        }

        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<bool> HasCompleteProfileAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var profile = await GetOwnAsync(actor, cancellationToken);
        return profile is not null
               && !string.IsNullOrWhiteSpace(profile.FirstName)
               && !string.IsNullOrWhiteSpace(profile.LastName);
    }

    public async Task<BuergerProfil> RequireSubmittingCitizenAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetOwnAsync(actor, cancellationToken)
            ?? throw new InvalidOperationException("Bitte zuerst Vor- und Nachnamen angeben.");

        if (string.IsNullOrWhiteSpace(profile.FirstName) || string.IsNullOrWhiteSpace(profile.LastName))
        {
            throw new InvalidOperationException("Bitte zuerst Vor- und Nachnamen angeben.");
        }
        if (profile.IsBlocked)
        {
            throw new UnauthorizedAccessException(
                "Dieses Konto ist für Einreichungen gesperrt. Der öffentliche Bereich bleibt lesbar.");
        }
        return profile;
    }

    public async Task<IReadOnlyList<CitizenRow>> ListAsync(ClaimsPrincipal actor, string? search = null,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.BuergerProfile.AsNoTracking().Include(p => p.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.FirstName.Contains(term)
                                     || p.LastName.Contains(term)
                                     || (p.User != null && p.User.DiscordUsername != null
                                                        && p.User.DiscordUsername.Contains(term)));
        }

        var rows = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(p => new CitizenRow(
            p.Id, p.UserId, p.FirstName, p.LastName, p.User?.DiscordUsername,
            p.IsBlocked, p.BlockedReason, p.BlockedAt, p.ConfirmedTips, p.LinkedPersonId,
            p.User?.RegisteredAt ?? p.CreatedAt)).ToList();
    }

    public async Task BlockAsync(string profileId, string reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Bitte einen Sperrgrund angeben.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrThrowAsync(db, profileId, cancellationToken);
        profile.IsBlocked = true;
        profile.BlockedReason = reason.Trim();
        profile.BlockedById = actor.GetAgentId();
        profile.BlockedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockAsync(string profileId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrThrowAsync(db, profileId, cancellationToken);
        profile.IsBlocked = false;
        // reason and timestamp stay: the history of a lifted block is worth keeping
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LinkPersonAsync(string profileId, string? personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await GetOrThrowAsync(db, profileId, cancellationToken);

        if (string.IsNullOrWhiteSpace(personId))
        {
            profile.LinkedPersonId = null;
        }
        else
        {
            // the visible read path, not a bare existence check: a classified file must not be linkable blind
            if (!await Visibility.IsRecordVisibleAsync(db, nameof(Person), personId,
                    ViewerScope.From(actor), cancellationToken))
            {
                throw new InvalidOperationException("Die ausgewählte Personenakte wurde nicht gefunden.");
            }
            profile.LinkedPersonId = personId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LinkedPersonInfo?> GetLinkedPersonAsync(string profileId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var personId = await db.BuergerProfile.AsNoTracking()
            .Where(p => p.Id == profileId).Select(p => p.LinkedPersonId)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(personId))
        {
            return null;
        }

        var person = await db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == personId, cancellationToken);
        if (person is null || (person.IsClassified && !actor.MayClassifiedRead()))
        {
            return null;
        }
        return new LinkedPersonInfo(person.Id, person.Name, person.CaseNumber, person.ThreatScore,
            person.ThreatConfidence, person.ScoreCalculatedAt, person.IsClassified);
    }

    public async Task RecomputeConfirmedTipsAsync(string profileId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var confirmed = await db.Hinweise.AsNoTracking().Where(TipRules.ConfirmedRows)
            .CountAsync(h => h.CitizenProfileId == profileId, cancellationToken);

        // ExecuteUpdate, and only when it moved: a tracked write would audit-stamp the profile per tip decision
        await db.BuergerProfile.Where(p => p.Id == profileId && p.ConfirmedTips != confirmed)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConfirmedTips, confirmed), cancellationToken);
    }

    private static async Task<BuergerProfil> GetOrThrowAsync(AppDbContext db, string profileId,
        CancellationToken cancellationToken)
        => await db.BuergerProfile.FirstOrDefaultAsync(p => p.Id == profileId, cancellationToken)
           ?? throw new InvalidOperationException("Bürgerkonto nicht gefunden.");

    private static string Clean(string value, string label)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException($"Bitte einen {label} angeben.");
        }
        if (trimmed.Length > NameMaxLength)
        {
            throw new InvalidOperationException($"Der {label} darf höchstens {NameMaxLength} Zeichen lang sein.");
        }
        return trimmed;
    }
}
