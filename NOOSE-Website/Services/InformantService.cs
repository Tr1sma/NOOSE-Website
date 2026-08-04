using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Informants;
using NOOSE_Website.Models.Informants;

namespace NOOSE_Website.Services;

/// <summary>Confidential informant management with strict two-tier secrecy (codename vs. real identity).</summary>
public interface IInformantService
{
    Task<List<InformantDisplay>> GetListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<InformantDisplay?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<InformantMeetingDisplay>> GetMeetingsAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task AddMeetingAsync(string id, InformantMeetingInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<InformantHandlerOption>> GetHandlerOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
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
            .Select(i => new { i.Id, i.CaseNumber, i.Codename, i.Description, i.Reliability, i.Status, i.HandlerId })
            .ToListAsync(cancellationToken);
        var handlers = await HandlerNamesAsync(db, rows.Select(r => r.HandlerId), cancellationToken);

        // NB: the list never carries identity fields (RealName stays null regardless of tier)
        return rows
            .Select(r => new InformantDisplay(
                r.Id, r.CaseNumber, r.Codename, r.Description, r.Reliability, r.Status,
                r.HandlerId, handlers.GetValueOrDefault(r.HandlerId),
                InformantVisibility.MaySeeIdentity(actor, r.HandlerId), null, null, null,
                InformantVisibility.MayWrite(actor, r.HandlerId)))
            .OrderBy(d => d.Codename)
            .ToList();
    }

    public async Task<InformantDisplay?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var inf = await db.Informants.Where(i => i.Id == id)
            .Select(i => new { i.Id, i.CaseNumber, i.Codename, i.Description, i.Reliability, i.Status, i.HandlerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (inf is null || !InformantVisibility.MaySeeRecord(actor, inf.HandlerId))
        {
            return null;
        }
        var handlerName = await db.Users.Where(u => u.Id == inf.HandlerId).Select(u => u.Codename).FirstOrDefaultAsync(cancellationToken);

        var maySeeId = InformantVisibility.MaySeeIdentity(actor, inf.HandlerId);
        string? realName = null, contact = null, notes = null;
        if (maySeeId)
        {
            // identity is only READ when authorized — never loaded into the DTO otherwise
            var idn = await db.InformantIdentities.Where(x => x.InformantId == id)
                .Select(x => new { x.RealName, x.ContactInfo, x.Notes }).FirstOrDefaultAsync(cancellationToken);
            realName = idn?.RealName;
            contact = idn?.ContactInfo;
            notes = idn?.Notes;
        }

        return new InformantDisplay(
            inf.Id, inf.CaseNumber, inf.Codename, inf.Description, inf.Reliability, inf.Status,
            inf.HandlerId, handlerName, maySeeId, realName, contact, notes,
            InformantVisibility.MayWrite(actor, inf.HandlerId));
    }

    public async Task<string> CreateAsync(InformantInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor); // creating + assigning a handler is a leadership act
        if (string.IsNullOrWhiteSpace(input.Codename))
        {
            throw new InvalidOperationException("Deckname erforderlich.");
        }
        if (string.IsNullOrWhiteSpace(input.HandlerId))
        {
            throw new InvalidOperationException("Führungsagent erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // case-number allocation needs an enclosing transaction (race-safety)
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var inf = new Informant
        {
            Codename = input.Codename.Trim(),
            Description = input.Description,
            Reliability = input.Reliability,
            Status = input.Status,
            HandlerId = input.HandlerId,
            CaseNumber = await caseNumbers.NextAsync(db, "VP", cancellationToken),
        };
        db.Informants.Add(inf);
        if (!string.IsNullOrWhiteSpace(input.RealName))
        {
            db.InformantIdentities.Add(new InformantIdentity
            {
                InformantId = inf.Id, RealName = input.RealName.Trim(), ContactInfo = input.ContactInfo, Notes = input.Notes,
            });
        }
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

        inf.Codename = input.Codename.Trim();
        inf.Description = input.Description;
        inf.Reliability = input.Reliability;
        inf.Status = input.Status;

        // reassigning the handler is leadership-only
        if (!string.IsNullOrWhiteSpace(input.HandlerId) && input.HandlerId != inf.HandlerId)
        {
            Permission.RequireLeadership(actor);
            inf.HandlerId = input.HandlerId;
        }

        // identity is only written by those who may see it
        if (InformantVisibility.MaySeeIdentity(actor, inf.HandlerId))
        {
            var idn = await db.InformantIdentities.FirstOrDefaultAsync(x => x.InformantId == id, cancellationToken);
            if (string.IsNullOrWhiteSpace(input.RealName))
            {
                if (idn is not null)
                {
                    db.InformantIdentities.Remove(idn);
                }
            }
            else if (idn is null)
            {
                db.InformantIdentities.Add(new InformantIdentity
                {
                    InformantId = id, RealName = input.RealName.Trim(), ContactInfo = input.ContactInfo, Notes = input.Notes,
                });
            }
            else
            {
                idn.RealName = input.RealName.Trim();
                idn.ContactInfo = input.ContactInfo;
                idn.Notes = input.Notes;
            }
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
        var authors = await HandlerNamesAsync(db, meetings.Where(m => m.CreatedById != null).Select(m => m.CreatedById!), cancellationToken);
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
        var rows = await db.Users
            .Where(u => u.Status == NOOSE_Website.Models.Enums.AgentStatus.Active)
            .Select(u => new { u.Id, u.Codename })
            .ToListAsync(cancellationToken);
        return rows
            .Select(u => new InformantHandlerOption(u.Id, string.IsNullOrWhiteSpace(u.Codename) ? u.Id : u.Codename!))
            .OrderBy(o => o.Name)
            .ToList();
    }

    private static async Task<Dictionary<string, string?>> HandlerNamesAsync(AppDbContext db, IEnumerable<string> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<string, string?>();
        }
        return await db.Users.Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, u.Codename })
            .ToDictionaryAsync(u => u.Id, u => (string?)u.Codename, ct);
    }
}
