using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBewerbungssperreService" />
public class BewerbungssperreService(IDbContextFactory<AppDbContext> dbFactory) : IBewerbungssperreService
{
    private static readonly TimeSpan BanDuration = TimeSpan.FromDays(14);

    public async Task<BewerbungssperreInfo?> GetActiveAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return null;
        }
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sperre = await db.Bewerbungssperren.AsNoTracking()
            .Where(s => s.AgentId == agentId && (s.IsBlacklist || s.BannedUntil > now))
            .OrderByDescending(s => s.IsBlacklist)
            .ThenByDescending(s => s.BannedUntil)
            .FirstOrDefaultAsync(cancellationToken);
        return sperre is null ? null : Map(sperre);
    }

    public async Task<List<BewerbungssperreInfo>> ListActiveAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Bewerbungssperren.AsNoTracking()
            .Where(s => s.IsBlacklist || s.BannedUntil > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task BanAsync(string agentId, string? bewerbungId, string? applicantName, string? reason,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        Permission.RequireWriteAccess(actor);
        if (string.IsNullOrEmpty(agentId))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var until = now + BanDuration;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var active = await db.Bewerbungssperren
            .Where(s => s.AgentId == agentId && (s.IsBlacklist || s.BannedUntil > now))
            .ToListAsync(cancellationToken);

        // a permanent blacklist already covers the applicant — nothing stronger to do
        if (active.Any(s => s.IsBlacklist))
        {
            return;
        }

        var temp = active.FirstOrDefault();
        if (temp is not null)
        {
            temp.BannedUntil = until;
            temp.Reason = Trim(reason) ?? temp.Reason;
        }
        else
        {
            db.Bewerbungssperren.Add(new Bewerbungssperre
            {
                AgentId = agentId,
                DiscordId = await DiscordIdAsync(db, agentId, cancellationToken),
                ApplicantName = Trim(applicantName),
                BewerbungId = bewerbungId,
                IsBlacklist = false,
                BannedUntil = until,
                Reason = Trim(reason),
                CreatedByName = actor.GetCodename(),
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task BlacklistAsync(string agentId, string? bewerbungId, string? applicantName, string? reason,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        Permission.RequireWriteAccess(actor);
        if (string.IsNullOrEmpty(agentId))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var active = await db.Bewerbungssperren
            .Where(s => s.AgentId == agentId && (s.IsBlacklist || s.BannedUntil > now))
            .ToListAsync(cancellationToken);

        if (active.Any(s => s.IsBlacklist))
        {
            return;
        }

        // supersede any temporary ban with the permanent blacklist
        foreach (var temp in active)
        {
            db.Bewerbungssperren.Remove(temp);
        }
        db.Bewerbungssperren.Add(new Bewerbungssperre
        {
            AgentId = agentId,
            DiscordId = await DiscordIdAsync(db, agentId, cancellationToken),
            ApplicantName = Trim(applicantName),
            BewerbungId = bewerbungId,
            IsBlacklist = true,
            BannedUntil = null,
            Reason = Trim(reason),
            CreatedByName = actor.GetCodename(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ShortenAsync(string sperreId, DateTime newUntil, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        Permission.RequireWriteAccess(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sperre = await GetOrThrow(db, sperreId, cancellationToken);
        if (sperre.IsBlacklist)
        {
            throw new InvalidOperationException("Blacklist-Einträge können nicht verkürzt werden.");
        }
        sperre.BannedUntil = newUntil;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LiftAsync(string sperreId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        Permission.RequireWriteAccess(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var sperre = await GetOrThrow(db, sperreId, cancellationToken);
        db.Bewerbungssperren.Remove(sperre); // interceptor soft-deletes
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Bewerbungssperre> GetOrThrow(AppDbContext db, string id, CancellationToken cancellationToken)
        => await db.Bewerbungssperren.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw new InvalidOperationException("Sperre nicht gefunden.");

    private static Task<string?> DiscordIdAsync(AppDbContext db, string agentId, CancellationToken cancellationToken)
        => db.Users.Where(u => u.Id == agentId).Select(u => u.DiscordId).FirstOrDefaultAsync(cancellationToken);

    private static BewerbungssperreInfo Map(Bewerbungssperre s)
        => new(s.Id, s.AgentId, s.DiscordId, s.ApplicantName, s.BewerbungId, s.IsBlacklist, s.BannedUntil, s.Reason, s.CreatedAt, s.CreatedByName);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
