using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Personal;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonalakteService" />
public class PersonalakteService(IDbContextFactory<AppDbContext> dbFactory) : IPersonalakteService
{
    public async Task<List<AgentDienstgradVerlauf>> GetDienstgradVerlaufAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentDienstgradVerlaeufe
            .Where(v => v.AgentId == agentId)
            .OrderByDescending(v => v.Zeitpunkt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AgentVermerk>> GetVermerkeAsync(string agentId, AgentVermerkArt art, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentVermerke
            .Where(v => v.AgentId == agentId && v.Art == art)
            .OrderByDescending(v => v.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentVermerk> VermerkErstellenAsync(string agentId, AgentVermerkArt art, string text, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);
        var inhalt = text?.Trim();
        if (string.IsNullOrEmpty(inhalt))
        {
            throw new InvalidOperationException("Der Vermerk darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        var vermerk = new AgentVermerk
        {
            AgentId = agentId,
            Art = art,
            Text = inhalt,
            AutorName = handelnder.GetCodename(),
        };
        db.AgentVermerke.Add(vermerk);
        await db.SaveChangesAsync(cancellationToken);
        return vermerk;
    }

    public async Task VermerkLoeschenAsync(string vermerkId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vermerk = await db.AgentVermerke.FirstOrDefaultAsync(v => v.Id == vermerkId, cancellationToken);
        if (vermerk is null)
        {
            return;
        }
        // Löschen darf der Verfasser selbst oder die Führung – serverseitig erzwingen.
        if (!handelnder.IstFuehrung() && vermerk.ErstelltVonId != handelnder.GetAgentId())
        {
            throw new UnauthorizedAccessException("Diesen Vermerk darf nur der Verfasser oder die Führung löschen.");
        }
        db.AgentVermerke.Remove(vermerk); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AgentBefoerderungsantrag>> GetBefoerderungsantraegeAsync(string agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentBefoerderungsantraege
            .Where(a => a.AgentId == agentId)
            .OrderByDescending(a => a.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AgentBefoerderungsantrag>> GetOffeneBefoerderungsantraegeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AgentBefoerderungsantraege
            .Where(a => a.Status == BefoerderungStatus.Beantragt)
            .OrderBy(a => a.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<AgentBefoerderungsantrag> BefoerderungBeantragenAsync(string agentId, Dienstgrad zielDienstgrad, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.AgentBefoerderungsantraege.AnyAsync(a => a.AgentId == agentId && a.Status == BefoerderungStatus.Beantragt, cancellationToken))
        {
            throw new InvalidOperationException("Für diesen Agenten ist bereits ein Beförderungsantrag offen.");
        }
        var antrag = new AgentBefoerderungsantrag
        {
            AgentId = agentId,
            ZielDienstgrad = zielDienstgrad,
            Begruendung = string.IsNullOrWhiteSpace(begruendung) ? null : begruendung.Trim(),
            Status = BefoerderungStatus.Beantragt,
            AntragstellerName = handelnder.GetCodename(),
        };
        db.AgentBefoerderungsantraege.Add(antrag);
        await db.SaveChangesAsync(cancellationToken);
        return antrag;
    }
}
