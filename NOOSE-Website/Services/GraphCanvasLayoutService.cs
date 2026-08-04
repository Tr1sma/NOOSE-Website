using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>Per-agent saved graph canvas layouts (node positions). Owned, hard-deletable.</summary>
public interface IGraphCanvasLayoutService
{
    Task<List<GraphCanvasLayout>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default);
    Task<GraphCanvasLayout> SaveAsync(string name, string layoutJson, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IGraphCanvasLayoutService" />
public class GraphCanvasLayoutService(IDbContextFactory<AppDbContext> dbFactory) : IGraphCanvasLayoutService
{
    public async Task<List<GraphCanvasLayout>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.GraphCanvasLayouts
            .Where(g => g.AgentId == agentId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<GraphCanvasLayout> SaveAsync(string name, string layoutJson, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Bitte einen Namen für die Ansicht angeben.");
        }
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new InvalidOperationException("Kein angemeldeter Agent.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // upsert by (agent, name): re-saving the same name overwrites the positions
        var entry = await db.GraphCanvasLayouts.FirstOrDefaultAsync(
            g => g.AgentId == agentId && g.Name == name, cancellationToken);
        if (entry is null)
        {
            entry = new GraphCanvasLayout { AgentId = agentId, Name = name, LayoutJson = layoutJson };
            db.GraphCanvasLayouts.Add(entry);
        }
        else
        {
            entry.LayoutJson = layoutJson;
        }
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        var agentId = actor.GetAgentId();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // only own layouts deletable
        var entry = await db.GraphCanvasLayouts.FirstOrDefaultAsync(g => g.Id == id && g.AgentId == agentId, cancellationToken);
        if (entry is null)
        {
            return;
        }
        db.GraphCanvasLayouts.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
