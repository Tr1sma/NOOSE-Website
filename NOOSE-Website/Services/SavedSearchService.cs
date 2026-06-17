using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISavedSearchService" />
public class SavedSearchService(IDbContextFactory<AppDbContext> dbFactory) : ISavedSearchService
{
    public async Task<List<SavedSearch>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavedSearch
            .Where(g => g.AgentId == agentId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<SavedSearch> SaveAsync(string agentId, string name, SearchCriteria criteria, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Bitte einen Namen für die Suche angeben.");
        }
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new InvalidOperationException("Kein angemeldeter Agent.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = new SavedSearch
        {
            AgentId = agentId,
            Name = name,
            SearchParameterJson = JsonSerializer.Serialize(criteria),
        };
        db.SavedSearch.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task DeleteAsync(string id, string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // only own searches deletable
        var entry = await db.SavedSearch.FirstOrDefaultAsync(g => g.Id == id && g.AgentId == agentId, cancellationToken);
        if (entry is null)
        {
            return;
        }
        db.SavedSearch.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
