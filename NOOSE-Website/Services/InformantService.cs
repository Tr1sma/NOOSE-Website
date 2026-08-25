using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Informants;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Informants;

namespace NOOSE_Website.Services;

/// <summary>Confidential informant management. Informants carry a real name only (no codename) and may be linked to a
/// person record and a faction. Every internal agent may do everything with every record; partners see nothing.</summary>
public interface IInformantService
{
    Task<List<InformantDisplay>> GetListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<InformantDisplay?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Move the informant to the trash (soft delete); any internal agent may.</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Deleted informants for the trash page; gated by that page, like every other record type.</summary>
    Task<List<InformantTrashItem>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>Restore from the trash — leadership only, same as every other record type.</summary>
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<InformantMeetingDisplay>> GetMeetingsAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task AddMeetingAsync(string id, InformantMeetingInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<InformantHandlerOption>> GetHandlerOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Informant marker for a person record, or null when the person is not an informant (or the viewer is a partner).</summary>
    Task<InformantPersonMarker?> GetPersonMarkerAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Informants linked to a faction; empty for partners.</summary>
    Task<List<InformantFactionEntry>> GetForFactionAsync(string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IInformantService" />
public class InformantService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumbers) : IInformantService
{
    public async Task<List<InformantDisplay>> GetListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!InformantVisibility.MaySeeRecord(actor))
        {
            return new List<InformantDisplay>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Informants
            .Select(i => new
            {
                i.Id, i.CaseNumber, i.RealName, i.PersonId, i.FactionId, i.Description,
                i.Reliability, i.Status, i.HandlerId, i.ContactInfo, i.Notes,
            })
            .ToListAsync(cancellationToken);
        var handlers = await HandlerDisplayAsync(db, rows.Select(r => r.HandlerId), actor.MayRealNameSee(), cancellationToken);
        var people = await LinkedPeopleAsync(db, rows.Select(r => r.PersonId), actor, cancellationToken);
        var factions = await LinkedFactionsAsync(db, rows.Select(r => r.FactionId), actor, cancellationToken);
        var mayEdit = actor.MayWrite();

        return rows
            .Select(r =>
            {
                var person = r.PersonId is null ? null : people.GetValueOrDefault(r.PersonId);
                return new InformantDisplay(
                    r.Id, r.CaseNumber, Label(person?.Name, r.RealName, r.CaseNumber), r.Description, r.Reliability, r.Status,
                    r.HandlerId, handlers.GetValueOrDefault(r.HandlerId),
                    person?.Id, person?.Name, person?.CaseNumber,
                    r.FactionId, r.FactionId is null ? null : factions.GetValueOrDefault(r.FactionId),
                    r.ContactInfo, r.Notes,
                    mayEdit);
            })
            .OrderBy(d => d.Name)
            .ToList();
    }

    public async Task<InformantDisplay?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!InformantVisibility.MaySeeRecord(actor))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id, i.CaseNumber, i.RealName, i.PersonId, i.FactionId, i.Description,
                i.Reliability, i.Status, i.HandlerId, i.ContactInfo, i.Notes,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (inf is null)
        {
            return null;
        }
        var handlerName = (await HandlerDisplayAsync(db, new[] { inf.HandlerId }, actor.MayRealNameSee(), cancellationToken))
            .GetValueOrDefault(inf.HandlerId);
        var people = await LinkedPeopleAsync(db, new[] { inf.PersonId }, actor, cancellationToken);
        var person = inf.PersonId is null ? null : people.GetValueOrDefault(inf.PersonId);
        var factions = await LinkedFactionsAsync(db, new[] { inf.FactionId }, actor, cancellationToken);

        return new InformantDisplay(
            inf.Id, inf.CaseNumber, Label(person?.Name, inf.RealName, inf.CaseNumber), inf.Description,
            inf.Reliability, inf.Status, inf.HandlerId, handlerName,
            person?.Id, person?.Name, person?.CaseNumber,
            inf.FactionId, inf.FactionId is null ? null : factions.GetValueOrDefault(inf.FactionId),
            inf.ContactInfo, inf.Notes,
            actor.MayWrite());
    }

    public async Task<string> CreateAsync(InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        if (string.IsNullOrWhiteSpace(input.HandlerId))
        {
            throw new InvalidOperationException("Führungsagent erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSelectableHandlerAsync(db, input.HandlerId, cancellationToken);
        var (personId, realName) = await ResolveNameSourceAsync(db, input, null, null, actor, cancellationToken);
        var factionId = await ResolveFactionAsync(db, input.FactionId, actor, cancellationToken);

        // case-number allocation needs an enclosing transaction (race-safety)
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var inf = new Informant
        {
            RealName = realName,
            PersonId = personId,
            FactionId = factionId,
            Description = input.Description,
            ContactInfo = input.ContactInfo,
            Notes = input.Notes,
            Reliability = input.Reliability,
            Status = input.Status,
            HandlerId = input.HandlerId,
            CaseNumber = await caseNumbers.NextAsync(db, "VP", cancellationToken),
        };
        db.Informants.Add(inf);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return inf.Id;
    }

    public async Task UpdateAsync(string id, InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (inf is null)
        {
            return;
        }

        var (personId, realName) = await ResolveNameSourceAsync(db, input, id, inf.PersonId, actor, cancellationToken);
        inf.RealName = realName;
        inf.PersonId = personId;
        inf.FactionId = await ResolveFactionAsync(db, input.FactionId, actor, cancellationToken);
        inf.Description = input.Description;
        inf.ContactInfo = input.ContactInfo;
        inf.Notes = input.Notes;
        inf.Reliability = input.Reliability;
        inf.Status = input.Status;

        // an empty handler would orphan the record, so only a real id reassigns it
        if (!string.IsNullOrWhiteSpace(input.HandlerId) && input.HandlerId != inf.HandlerId)
        {
            await EnsureSelectableHandlerAsync(db, input.HandlerId, cancellationToken);
            inf.HandlerId = input.HandlerId;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (inf is null)
        {
            return;
        }
        // soft delete via interceptor; the meetings stay put so a restore brings the file back whole
        db.Informants.Remove(inf);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<InformantTrashItem>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Informants.AsNoTracking().IgnoreQueryFilters()
            .Where(i => i.IsDeleted)
            .OrderByDescending(i => i.DeletedAt)
            .Select(i => new { i.Id, i.CaseNumber, i.RealName, i.PersonId, i.Status, i.DeletedAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new List<InformantTrashItem>();
        }
        // no classification filter: every viewer of the trash page may read classified records anyway
        var personIds = rows.Where(r => r.PersonId != null).Select(r => r.PersonId!).Distinct().ToList();
        var people = await db.People.AsNoTracking().Where(p => personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return rows
            .Select(r => new InformantTrashItem(
                r.Id, r.CaseNumber,
                Label(r.PersonId is null ? null : people.GetValueOrDefault(r.PersonId), r.RealName, r.CaseNumber),
                InformantEnumDisplay.Status(r.Status), r.DeletedAt))
            .ToList();
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Informant '{id}' nicht gefunden.");

        inf.IsDeleted = false;
        inf.DeletedAt = null;
        inf.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<InformantMeetingDisplay>> GetMeetingsAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!InformantVisibility.MaySeeRecord(actor))
        {
            return new List<InformantMeetingDisplay>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meetings = await db.InformantMeetings.Where(m => m.InformantId == id)
            .OrderByDescending(m => m.MeetingDate)
            .Select(m => new { m.Id, m.MeetingDate, m.Location, m.Content, m.CreatedById })
            .ToListAsync(cancellationToken);
        var authors = await HandlerDisplayAsync(db, meetings.Where(m => m.CreatedById != null).Select(m => m.CreatedById!), actor.MayRealNameSee(), cancellationToken);
        return meetings
            .Select(m => new InformantMeetingDisplay(m.Id, m.MeetingDate, m.Location, m.Content,
                m.CreatedById is null ? null : authors.GetValueOrDefault(m.CreatedById)))
            .ToList();
    }

    public async Task AddMeetingAsync(string id, InformantMeetingInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Informants.AnyAsync(i => i.Id == id, cancellationToken))
        {
            return;
        }
        db.InformantMeetings.Add(new InformantMeeting
        {
            InformantId = id, MeetingDate = input.MeetingDate, Location = input.Location, Content = input.Content,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<InformantHandlerOption>> GetHandlerOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Users.OnlySelectable()
            .Select(u => new { u.Id, u.Codename, u.RealName })
            .ToListAsync(cancellationToken);
        return rows
            .Select(u => new InformantHandlerOption(u.Id, AgentNameDisplay.Pick(u.Codename, u.RealName, actor.MayRealNameSee())))
            .OrderBy(o => o.Name)
            .ToList();
    }

    public async Task<InformantPersonMarker?> GetPersonMarkerAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!InformantVisibility.MaySeeRecord(actor) || string.IsNullOrWhiteSpace(personId))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Informants.Where(i => i.PersonId == personId)
            .Select(i => new { i.Id, i.CaseNumber, i.Status })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new InformantPersonMarker(row.Id, row.CaseNumber, row.Status);
    }

    public async Task<List<InformantFactionEntry>> GetForFactionAsync(string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!InformantVisibility.MaySeeRecord(actor) || string.IsNullOrWhiteSpace(factionId))
        {
            return new List<InformantFactionEntry>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Informants.Where(i => i.FactionId == factionId)
            .Select(i => new { i.Id, i.CaseNumber, i.RealName, i.PersonId, i.Status })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new List<InformantFactionEntry>();
        }
        var people = await LinkedPeopleAsync(db, rows.Select(r => r.PersonId), actor, cancellationToken);

        return rows
            .Select(r =>
            {
                var person = r.PersonId is null ? null : people.GetValueOrDefault(r.PersonId);
                return new InformantFactionEntry(
                    r.Id, r.CaseNumber, Label(person?.Name, r.RealName, r.CaseNumber), r.Status);
            })
            .OrderBy(e => e.Name)
            .ToList();
    }

    // Keep the write path exactly as wide as the picker; an unselectable handler would surface a hidden account.
    private static async Task EnsureSelectableHandlerAsync(AppDbContext db, string handlerId, CancellationToken ct)
    {
        if (!await db.Users.OnlySelectable().AnyAsync(u => u.Id == handlerId, ct))
        {
            throw new InvalidOperationException("Führungsagent nicht gefunden oder nicht auswählbar.");
        }
    }

    // Decide where the informant's name comes from: a linked person record wins, otherwise the free-text real name.
    private static async Task<(string? PersonId, string? RealName)> ResolveNameSourceAsync(
        AppDbContext db, InformantInput input, string? informantId, string? currentPersonId,
        ClaimsPrincipal actor, CancellationToken ct)
    {
        var personId = string.IsNullOrWhiteSpace(input.PersonId) ? null : input.PersonId.Trim();
        var realName = string.IsNullOrWhiteSpace(input.RealName) ? null : input.RealName.Trim();

        if (personId is not null)
        {
            var mayClassified = actor.MayClassifiedRead();
            var exists = await db.People
                .AnyAsync(p => p.Id == personId && (mayClassified || !p.IsClassified), ct);
            if (!exists)
            {
                throw new InvalidOperationException("Personenakte nicht gefunden oder nicht zugänglich.");
            }
            // the unique index also counts soft-deleted informants, so bypass the filter here
            var taken = await db.Informants.IgnoreQueryFilters()
                .AnyAsync(i => i.PersonId == personId && (informantId == null || i.Id != informantId), ct);
            if (taken)
            {
                throw new InvalidOperationException(
                    "Diese Personenakte ist bereits einem Informanten zugeordnet (ggf. im Papierkorb).");
            }
            return (personId, null); // the record is the single source of the name
        }

        if (realName is null && currentPersonId is not null)
        {
            // unlinking must not leave a nameless informant — carry the person's name over
            realName = await db.People.Where(p => p.Id == currentPersonId).Select(p => p.Name).FirstOrDefaultAsync(ct);
        }
        if (string.IsNullOrWhiteSpace(realName))
        {
            throw new InvalidOperationException("Klarname oder verknüpfte Personenakte erforderlich.");
        }
        return (null, realName);
    }

    // Validate the optional faction link; unlike the person link this one is not exclusive.
    private static async Task<string?> ResolveFactionAsync(
        AppDbContext db, string? rawFactionId, ClaimsPrincipal actor, CancellationToken ct)
    {
        var factionId = string.IsNullOrWhiteSpace(rawFactionId) ? null : rawFactionId.Trim();
        if (factionId is null)
        {
            return null;
        }
        var mayClassified = actor.MayClassifiedRead();
        var exists = await db.Factions.AnyAsync(f => f.Id == factionId && (mayClassified || !f.IsClassified), ct);
        if (!exists)
        {
            throw new InvalidOperationException("Fraktionsakte nicht gefunden oder nicht zugänglich.");
        }
        return factionId;
    }

    private sealed record LinkedPerson(string Id, string Name, string CaseNumber);

    // Resolve linked person ids to name + case number; classified records only for viewers allowed to read them.
    private static async Task<Dictionary<string, LinkedPerson>> LinkedPeopleAsync(
        AppDbContext db, IEnumerable<string?> personIds, ClaimsPrincipal actor, CancellationToken ct)
    {
        var list = personIds.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, LinkedPerson>();
        }
        var mayClassified = actor.MayClassifiedRead();
        var rows = await db.People.Where(p => list.Contains(p.Id) && (mayClassified || !p.IsClassified))
            .Select(p => new LinkedPerson(p.Id, p.Name, p.CaseNumber))
            .ToListAsync(ct);
        return rows.ToDictionary(p => p.Id);
    }

    // Resolve linked faction ids to their names, under the same classification gate as people.
    private static async Task<Dictionary<string, string>> LinkedFactionsAsync(
        AppDbContext db, IEnumerable<string?> factionIds, ClaimsPrincipal actor, CancellationToken ct)
    {
        var list = factionIds.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f!).Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, string>();
        }
        var mayClassified = actor.MayClassifiedRead();
        var rows = await db.Factions.Where(f => list.Contains(f.Id) && (mayClassified || !f.IsClassified))
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);
        return rows.ToDictionary(f => f.Id, f => f.Name);
    }

    // Display name of an informant: linked record first, then the free-text real name, never an empty label.
    private static string Label(string? personName, string? realName, string caseNumber)
        => !string.IsNullOrWhiteSpace(personName) ? personName!
            : !string.IsNullOrWhiteSpace(realName) ? realName!
            : caseNumber;

    // Resolve agent ids to a display name: codename first; real name only for viewers allowed to see it; never a raw id.
    private static async Task<Dictionary<string, string>> HandlerDisplayAsync(
        AppDbContext db, IEnumerable<string> ids, bool mayRealName, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, string>();
        }
        var rows = await db.Users.Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, u.Codename, u.RealName })
            .ToListAsync(ct);
        return rows.ToDictionary(u => u.Id, u => AgentNameDisplay.Pick(u.Codename, u.RealName, mayRealName));
    }
}
