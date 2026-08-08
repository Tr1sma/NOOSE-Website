using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Informants;
using NOOSE_Website.Models.Informants;

namespace NOOSE_Website.Services;

/// <summary>Confidential informant management. Informants carry a real name only (no codename) and may be linked to a
/// person record and a faction. Record access is all-or-nothing.</summary>
public interface IInformantService
{
    Task<List<InformantDisplay>> GetListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<InformantDisplay?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<InformantMeetingDisplay>> GetMeetingsAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task AddMeetingAsync(string id, InformantMeetingInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<InformantHandlerOption>> GetHandlerOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Informant marker for a person record, or null when the person is not an informant.</summary>
    Task<InformantPersonMarker?> GetPersonMarkerAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Informants linked to a faction; empty for partners.</summary>
    Task<List<InformantFactionEntry>> GetForFactionAsync(string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IInformantService" />
public class InformantService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumbers) : IInformantService
{
    public async Task<List<InformantDisplay>> GetListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = await InformantVisibility.VisibleIdsAsync(db, actor, cancellationToken);
        if (ids.Count == 0)
        {
            return new List<InformantDisplay>();
        }
        var rows = await db.Informants.Where(i => ids.Contains(i.Id))
            .Select(i => new
            {
                i.Id, i.CaseNumber, i.RealName, i.PersonId, i.FactionId, i.Description,
                i.Reliability, i.Status, i.HandlerId, i.ContactInfo, i.Notes,
            })
            .ToListAsync(cancellationToken);
        var handlers = await HandlerDisplayAsync(db, rows.Select(r => r.HandlerId), actor.MayRealNameSee(), cancellationToken);
        var people = await LinkedPeopleAsync(db, rows.Select(r => r.PersonId), actor, cancellationToken);
        var factions = await LinkedFactionsAsync(db, rows.Select(r => r.FactionId), actor, cancellationToken);

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
                    InformantVisibility.MayWrite(actor, r.HandlerId));
            })
            .OrderBy(d => d.Name)
            .ToList();
    }

    public async Task<InformantDisplay?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id, i.CaseNumber, i.RealName, i.PersonId, i.FactionId, i.Description,
                i.Reliability, i.Status, i.HandlerId, i.ContactInfo, i.Notes,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (inf is null || !InformantVisibility.MaySeeRecord(actor, inf.HandlerId))
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
            InformantVisibility.MayWrite(actor, inf.HandlerId));
    }

    public async Task<string> CreateAsync(InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor); // creating + assigning a handler is a leadership act
        if (string.IsNullOrWhiteSpace(input.HandlerId))
        {
            throw new InvalidOperationException("Führungsagent erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (inf is null)
        {
            return;
        }
        Permission.RequireInformantWrite(actor, inf.HandlerId);

        var (personId, realName) = await ResolveNameSourceAsync(db, input, id, inf.PersonId, actor, cancellationToken);
        inf.RealName = realName;
        inf.PersonId = personId;
        inf.FactionId = await ResolveFactionAsync(db, input.FactionId, actor, cancellationToken);
        inf.Description = input.Description;
        inf.ContactInfo = input.ContactInfo;
        inf.Notes = input.Notes;
        inf.Reliability = input.Reliability;
        inf.Status = input.Status;

        // reassigning the handler is leadership-only
        if (!string.IsNullOrWhiteSpace(input.HandlerId) && input.HandlerId != inf.HandlerId)
        {
            Permission.RequireLeadership(actor);
            inf.HandlerId = input.HandlerId;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<InformantMeetingDisplay>> GetMeetingsAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var handlerId = await db.Informants.Where(i => i.Id == id).Select(i => i.HandlerId).FirstOrDefaultAsync(cancellationToken);
        if (handlerId is null || !InformantVisibility.MaySeeRecord(actor, handlerId))
        {
            return new List<InformantMeetingDisplay>();
        }
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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var handlerId = await db.Informants.Where(i => i.Id == id).Select(i => i.HandlerId).FirstOrDefaultAsync(cancellationToken);
        if (handlerId is null)
        {
            return;
        }
        Permission.RequireInformantWrite(actor, handlerId);
        db.InformantMeetings.Add(new InformantMeeting
        {
            InformantId = id, MeetingDate = input.MeetingDate, Location = input.Location, Content = input.Content,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<InformantHandlerOption>> GetHandlerOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Users.OnlySelectable()
            .Select(u => new { u.Id, u.Codename, u.RealName })
            .ToListAsync(cancellationToken);
        return rows
            .Select(u => new InformantHandlerOption(u.Id, PickName(u.Codename, u.RealName, actor.MayRealNameSee())))
            .OrderBy(o => o.Name)
            .ToList();
    }

    public async Task<InformantPersonMarker?> GetPersonMarkerAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // the marker itself is open to every internal agent; only opening the V-person file is tiered
        if (actor.IsPartner() || string.IsNullOrWhiteSpace(personId))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Informants.Where(i => i.PersonId == personId)
            .Select(i => new { i.Id, i.CaseNumber, i.Status, i.HandlerId })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new InformantPersonMarker(row.Id, row.CaseNumber, row.Status, InformantVisibility.MaySeeRecord(actor, row.HandlerId));
    }

    public async Task<List<InformantFactionEntry>> GetForFactionAsync(string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (actor.IsPartner() || string.IsNullOrWhiteSpace(factionId))
        {
            return new List<InformantFactionEntry>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Informants.Where(i => i.FactionId == factionId)
            .Select(i => new { i.Id, i.CaseNumber, i.RealName, i.PersonId, i.HandlerId, i.Status })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new List<InformantFactionEntry>();
        }
        var people = await LinkedPeopleAsync(db, rows.Select(r => r.PersonId), actor, cancellationToken);

        return rows
            .Select(r =>
            {
                var mayOpen = InformantVisibility.MaySeeRecord(actor, r.HandlerId);
                var person = r.PersonId is null ? null : people.GetValueOrDefault(r.PersonId);
                // without record access the roster stays anonymous — case number only
                var name = mayOpen ? Label(person?.Name, r.RealName, r.CaseNumber) : r.CaseNumber;
                return new InformantFactionEntry(r.Id, r.CaseNumber, name, r.Status, mayOpen);
            })
            .OrderBy(e => e.Name)
            .ToList();
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
        return rows.ToDictionary(u => u.Id, u => PickName(u.Codename, u.RealName, mayRealName));
    }

    private static string PickName(string? codename, string? realName, bool mayRealName)
        => !string.IsNullOrWhiteSpace(codename) ? codename!
            : mayRealName && !string.IsNullOrWhiteSpace(realName) ? realName!
            : "(unbenannt)";
}
