using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Watchlist;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IWatchlistService" />
public class WatchlistService(IDbContextFactory<AppDbContext> dbFactory) : IWatchlistService
{
    public async Task FolgenAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Nur folgen, was der Aufrufer auch sehen darf (Verschlusssache/Papierkorb/Personalakte-Gate serverseitig).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, handelnder.IstFuehrung(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }

        // Bereits aktiv gefolgt → nichts zu tun.
        var aktiv = await db.Watchlisten
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId,
                cancellationToken);
        if (aktiv is not null)
        {
            return;
        }

        // Eine früher entfolgte (soft-gelöschte) Zeile reaktivieren statt eine zweite anzulegen.
        var alt = await db.Watchlisten.IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId
                                      && w.IstGeloescht, cancellationToken);
        if (alt is not null)
        {
            alt.IstGeloescht = false;
            alt.GeloeschtAm = null;
            alt.GeloeschtVonId = null;
        }
        else
        {
            db.Watchlisten.Add(new WatchlistEintrag
            {
                AgentId = agentId,
                EntitaetTyp = entitaetTyp,
                EntitaetId = entitaetId,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EntfolgenAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var eintrag = await db.Watchlisten
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId,
                cancellationToken);
        if (eintrag is null)
        {
            return;
        }
        // Hard-Delete → der Audit-Interceptor wandelt es in einen Soft-Delete um.
        db.Watchlisten.Remove(eintrag);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IstGefolgtAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return false;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlisten
            .AnyAsync(w => w.AgentId == agentId && w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId,
                cancellationToken);
    }

    public async Task<List<WatchlistEintrag>> GetGefolgteAsync(ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlisten
            .Where(w => w.AgentId == agentId)
            .OrderByDescending(w => w.ErstelltAm)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<GefolgteAkte>> GetGefolgteAufgeloestAsync(ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        var agentId = handelnder.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new();
        }
        // Lese-Gate: die Nur-Lese-Aufsicht darf gefolgte VS-Akten einsehen (DarfVerschlusssacheLesen).
        var istFuehrung = handelnder.DarfVerschlusssacheLesen();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var eintraege = await db.Watchlisten
            .Where(w => w.AgentId == agentId)
            .OrderByDescending(w => w.ErstelltAm)
            .Select(w => new { w.EntitaetTyp, w.EntitaetId, w.ErstelltAm })
            .ToListAsync(cancellationToken);
        if (eintraege.Count == 0)
        {
            return new();
        }

        var refs = eintraege.Select(e => (e.EntitaetTyp, e.EntitaetId)).Distinct().ToList();
        // Gefolgte Taskforces nur auflösen, wenn der Aufrufer zugeteilt ist (oder alle sehen darf).
        var aufgeloest = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken,
            darfAlleTaskforces: handelnder.DarfAlleTaskforcesSehen(), meId: agentId);

        var ergebnis = new List<GefolgteAkte>(eintraege.Count);
        foreach (var e in eintraege)
        {
            // Sichtbarkeit ohne zusätzliche DB-Abfrage je Eintrag: AktenReferenz hat das Verschlusssache-Flag bereits
            // mitgeladen (nicht auflösbar = Papierkorb/unbekannt → nicht zugänglich). Personalakten (Agent) gelten als
            // Führungs-Inhalt (entspricht Sichtbarkeit.IstAkteSichtbarAsync). Spiegelt die Logik dort 1:1, nur in-memory.
            bool zugaenglich;
            if (aufgeloest.TryGetValue((e.EntitaetTyp, e.EntitaetId), out var a))
            {
                zugaenglich = e.EntitaetTyp == nameof(Agent) ? istFuehrung : (!a.Verschluss || istFuehrung);
            }
            else
            {
                zugaenglich = false;
            }
            ergebnis.Add(new GefolgteAkte(
                e.EntitaetTyp, e.EntitaetId,
                zugaenglich ? a.Anzeige : "(nicht mehr zugänglich)",
                zugaenglich ? a.Href : null,
                e.ErstelltAm,
                zugaenglich));
        }
        return ergebnis;
    }

    public async Task<List<string>> GetFollowerIdsAsync(string entitaetTyp, string entitaetId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlisten
            .Where(w => w.EntitaetTyp == entitaetTyp && w.EntitaetId == entitaetId)
            .Select(w => w.AgentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
