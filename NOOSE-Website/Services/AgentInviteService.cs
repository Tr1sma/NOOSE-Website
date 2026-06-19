using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAgentInviteService" />
public class AgentInviteService(IDbContextFactory<AppDbContext> dbFactory, UserManager<Agent> userManager)
    : IAgentInviteService
{
    public async Task<AgentInvite> CreateAsync(string? label, DateTime? expiresAt, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        var invite = new AgentInvite
        {
            Token = NewToken(),
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            CreatedByName = actor.GetCodename(),
            ExpiresAt = expiresAt,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.AgentInvites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        return invite;
    }

    public async Task<AgentInvite?> ValidateAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invite = await db.AgentInvites.AsNoTracking().FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
        return IsValid(invite) ? invite : null;
    }

    public async Task ConsumeAsync(string token, string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invite = await db.AgentInvites.FirstOrDefaultAsync(i => i.Token == token, cancellationToken);
        if (!IsValid(invite))
        {
            throw new InvalidOperationException("Einladungslink ungültig oder bereits verwendet.");
        }
        invite!.UsedByUserId = userId;
        invite.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RedeemForExistingAsync(string? token, string userId, CancellationToken cancellationToken = default)
    {
        var invite = await ValidateAsync(token, cancellationToken);
        if (invite is null)
        {
            return false;
        }

        var agent = await userManager.FindByIdAsync(userId);
        if (agent is null || agent.Status != AgentStatus.Applicant)
        {
            return false;
        }

        agent.Status = AgentStatus.Pending;
        var update = await userManager.UpdateAsync(agent);
        if (!update.Succeeded)
        {
            return false;
        }
        await userManager.UpdateSecurityStampAsync(agent);

        await ConsumeAsync(invite.Token, userId, cancellationToken);
        return true;
    }

    public async Task RevokeAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var invite = await db.AgentInvites.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invite is null)
        {
            return;
        }
        db.AgentInvites.Remove(invite); // soft-delete via interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AgentInvite>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentInvites.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static bool IsValid(AgentInvite? invite)
        => invite is { UsedAt: null } && (invite.ExpiresAt is null || invite.ExpiresAt > DateTime.UtcNow);

    private static string NewToken()
        => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
}
