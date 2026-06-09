using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IGespeicherteSucheService" />
public class GespeicherteSucheService(IDbContextFactory<AppDbContext> dbFactory) : IGespeicherteSucheService
{
    public async Task<List<GespeicherteSuche>> GetFuerAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.GespeicherteSuchen
            .Where(g => g.AgentId == agentId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<GespeicherteSuche> SpeichernAsync(string agentId, string name, SuchKriterien kriterien, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
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
        var eintrag = new GespeicherteSuche
        {
            AgentId = agentId,
            Name = name,
            SuchparameterJson = JsonSerializer.Serialize(kriterien),
        };
        db.GespeicherteSuchen.Add(eintrag);
        await db.SaveChangesAsync(cancellationToken);
        return eintrag;
    }

    public async Task LoeschenAsync(string id, string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Nur eigene Suchen löschbar.
        var eintrag = await db.GespeicherteSuchen.FirstOrDefaultAsync(g => g.Id == id && g.AgentId == agentId, cancellationToken);
        if (eintrag is null)
        {
            return;
        }
        db.GespeicherteSuchen.Remove(eintrag);
        await db.SaveChangesAsync(cancellationToken);
    }
}
