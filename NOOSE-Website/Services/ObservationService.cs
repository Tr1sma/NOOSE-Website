using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IObservationService" />
public class ObservationService(IDbContextFactory<AppDbContext> dbFactory) : IObservationService
{
    public async Task<List<ObservationAnzeige>> GetFuerPersonAsync(string personId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Eigenständige Sichtbarkeitsprüfung der Eltern-Person (nicht nur auf den Aufrufer verlassen).
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, nameof(Person), personId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var eintraege = await db.Observationen
            .Where(o => o.PersonId == personId)
            .OrderByDescending(o => o.Beginn)
            .ToListAsync(cancellationToken);
        return await ZuAnzeigeAsync(db, eintraege, istFuehrung, cancellationToken);
    }

    public async Task<List<ObservationAnzeige>> GetAlleAsync(bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var eintraege = await db.Observationen
            .Include(o => o.Person)
            // Der Soft-Delete-Filter setzt Person bei gelöschten Akten auf null → solche Einträge ausblenden.
            .Where(o => o.Person != null && (istFuehrung || !o.Person.IstVerschlusssache))
            .OrderByDescending(o => o.Beginn)
            .ToListAsync(cancellationToken);
        return await ZuAnzeigeAsync(db, eintraege, istFuehrung, cancellationToken);
    }

    public async Task<List<ObservationAnzeige>> GetFuerOrgAsync(string orgTyp, string orgId, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Sichtbarkeit der Organisations-Akte selbst prüfen.
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, orgTyp, orgId, istFuehrung, cancellationToken))
        {
            return new();
        }
        var eintraege = await db.Observationen
            .Include(o => o.Person)
            // Person == null → Akte im Papierkorb; Verschlusssache-Personen nur für Führung.
            .Where(o => o.OrgTyp == orgTyp && o.OrgId == orgId
                && o.Person != null && (istFuehrung || !o.Person.IstVerschlusssache))
            .OrderByDescending(o => o.Beginn)
            .ToListAsync(cancellationToken);
        return await ZuAnzeigeAsync(db, eintraege, istFuehrung, cancellationToken);
    }

    /// <summary>
    /// Reichert geladene Observationen mit Anzeigedaten an: den Deckname des beobachtenden Agents sowie
    /// Name/Aktenzeichen/Route der verknüpften Organisation. Beides wird je in einer Sammelabfrage aufgelöst;
    /// Organisationen sind Verschlusssache-gefiltert (<paramref name="istFuehrung"/>), gelöschte werden vom
    /// globalen Soft-Delete-Filter ausgeblendet. Nicht (mehr) auflösbare Verknüpfungen erhalten leere Felder.
    /// </summary>
    private static async Task<List<ObservationAnzeige>> ZuAnzeigeAsync(AppDbContext db, List<Observation> eintraege, bool istFuehrung, CancellationToken cancellationToken)
    {
        // Beobachtende Agents (Deckname) auflösen.
        var agentIds = eintraege.Where(o => o.BeobachtenderAgentId is not null).Select(o => o.BeobachtenderAgentId!).Distinct().ToList();
        var agenten = new Dictionary<string, string>();
        if (agentIds.Count > 0)
        {
            agenten = await db.Users
                .Where(u => agentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename })
                .ToDictionaryAsync(u => u.Id, u => u.Codename, cancellationToken);
        }

        var fraktionIds = eintraege.Where(o => o.OrgTyp == nameof(Fraktion) && o.OrgId is not null).Select(o => o.OrgId!).Distinct().ToList();
        var gruppenIds = eintraege.Where(o => o.OrgTyp == nameof(Personengruppe) && o.OrgId is not null).Select(o => o.OrgId!).Distinct().ToList();

        var fraktionen = new Dictionary<string, (string Name, string Aktenzeichen)>();
        if (fraktionIds.Count > 0)
        {
            fraktionen = await db.Fraktionen
                .Where(f => fraktionIds.Contains(f.Id) && (istFuehrung || !f.IstVerschlusssache))
                .Select(f => new { f.Id, f.Name, f.Aktenzeichen })
                .ToDictionaryAsync(f => f.Id, f => (f.Name, f.Aktenzeichen), cancellationToken);
        }

        var gruppen = new Dictionary<string, (string Name, string Aktenzeichen)>();
        if (gruppenIds.Count > 0)
        {
            gruppen = await db.Personengruppen
                .Where(g => gruppenIds.Contains(g.Id) && (istFuehrung || !g.IstVerschlusssache))
                .Select(g => new { g.Id, g.Name, g.Aktenzeichen })
                .ToDictionaryAsync(g => g.Id, g => (g.Name, g.Aktenzeichen), cancellationToken);
        }

        return eintraege.Select(o =>
        {
            string? agentName = o.BeobachtenderAgentId is not null
                && agenten.TryGetValue(o.BeobachtenderAgentId, out var cn) && !string.IsNullOrWhiteSpace(cn)
                ? cn : null;
            if (o.OrgId is not null && o.OrgTyp == nameof(Fraktion) && fraktionen.TryGetValue(o.OrgId, out var f))
            {
                return new ObservationAnzeige(o, agentName, f.Name, f.Aktenzeichen, $"/fraktionen/{o.OrgId}");
            }
            if (o.OrgId is not null && o.OrgTyp == nameof(Personengruppe) && gruppen.TryGetValue(o.OrgId, out var g))
            {
                return new ObservationAnzeige(o, agentName, g.Name, g.Aktenzeichen, $"/personengruppen/{o.OrgId}");
            }
            return new ObservationAnzeige(o, agentName, null, null, null);
        }).ToList();
    }

    public async Task<Observation> ErstellenAsync(string personId, ObservationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Personen.FirstOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new InvalidOperationException($"Person '{personId}' nicht gefunden.");
        if (person.IstVerschlusssache && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var orgId = Leer(eingabe.OrgId);
        var obs = new Observation
        {
            PersonId = personId,
            Beginn = eingabe.Beginn,
            Ende = eingabe.Ende,
            Ort = Leer(eingabe.Ort),
            Beobachtung = Leer(eingabe.Beobachtung),
            Ergebnis = Leer(eingabe.Ergebnis),
            BeobachtenderAgentId = Leer(eingabe.BeobachtenderAgentId),
            OrgId = orgId,
            // Kein verwaister Typ ohne Id.
            OrgTyp = orgId is null ? null : Leer(eingabe.OrgTyp),
        };

        db.Observationen.Add(obs);
        // Audit setzt ErstelltAm/Von automatisch über den Interceptor.
        await db.SaveChangesAsync(cancellationToken);
        return obs;
    }

    public async Task<Observation> AktualisierenAsync(string observationId, ObservationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var obs = await db.Observationen
            .Include(o => o.Person)
            .FirstOrDefaultAsync(o => o.Id == observationId, cancellationToken)
            ?? throw new InvalidOperationException($"Observation '{observationId}' nicht gefunden.");

        if (obs.Person?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        obs.Beginn = eingabe.Beginn;
        obs.Ende = eingabe.Ende;
        obs.Ort = Leer(eingabe.Ort);
        obs.Beobachtung = Leer(eingabe.Beobachtung);
        obs.Ergebnis = Leer(eingabe.Ergebnis);
        obs.BeobachtenderAgentId = Leer(eingabe.BeobachtenderAgentId);
        var orgId = Leer(eingabe.OrgId);
        obs.OrgId = orgId;
        obs.OrgTyp = orgId is null ? null : Leer(eingabe.OrgTyp);

        // Audit setzt GeaendertAm/Von automatisch über den Interceptor.
        await db.SaveChangesAsync(cancellationToken);
        return obs;
    }

    public async Task LoeschenAsync(string observationId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var obs = await db.Observationen.Include(o => o.Person).FirstOrDefaultAsync(o => o.Id == observationId, cancellationToken);
        if (obs is null)
        {
            return;
        }
        if (obs.Person?.IstVerschlusssache == true && !handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Soft-Delete via Interceptor.
        db.Observationen.Remove(obs);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Leer(string? s) => s.TrimToNull();
}
