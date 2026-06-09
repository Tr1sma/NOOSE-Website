using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISearchService" />
public class SearchService(IDbContextFactory<AppDbContext> dbFactory) : ISearchService
{
    private const int MaxProKategorie = 50;

    public async Task<List<SuchErgebnisGruppe>> SuchenAsync(SuchKriterien kriterien, bool istFuehrung, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var s = kriterien.Text?.Trim();
        var hatText = !string.IsNullOrEmpty(s);
        var tagIds = kriterien.TagIds ?? new();
        var hatTags = tagIds.Count > 0;

        // Bewusst KEIN Früh-Ausstieg bei leerem Text/leeren Tags: ohne Filter sollen alle (sichtbaren)
        // Personen erscheinen (Durchblättern). Die Personen-Query unten lässt dann einfach das Text-Where
        // weg; die reinen Text-Kategorien (Doks/Quellen/Kommentare) bleiben mangels Suchtext leer.

        var kategorien = kriterien.Kategorien is { Count: > 0 } ? kriterien.Kategorien.ToHashSet() : null;
        bool Aktiv(string kat) => kategorien is null || kategorien.Contains(kat);

        var gruppen = new List<SuchErgebnisGruppe>();

        // ---- Personen (Name/Aktenzeichen/Beschreibung/Aliase; auch reiner Tag-Filter) ----
        if (Aktiv(nameof(Person)))
        {
            var q = db.Personen.Where(p => istFuehrung || !p.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(p => p.Name.Contains(s!) || p.Aktenzeichen.Contains(s!)
                    || (p.Beschreibung != null && p.Beschreibung.Contains(s!))
                    || p.Aliase.Any(a => a.Aliasname.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(p => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(p => p.Name).Take(MaxProKategorie)
                .Select(p => new SuchTreffer(nameof(Person), p.Id, p.Name,
                    p.Beschreibung ?? string.Empty, p.Aktenzeichen))
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Person), "Personen", treffer));
            }
        }

        // ---- Fraktionen (Name/Aktenzeichen/Art; auch reiner Tag-Filter) ----
        if (Aktiv(nameof(Fraktion)))
        {
            var q = db.Fraktionen.Where(f => istFuehrung || !f.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(f => f.Name.Contains(s!) || f.Aktenzeichen.Contains(s!)
                    || (f.Art != null && f.Art.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(f => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Fraktion) && z.EntitaetId == f.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(f => f.Name).Take(MaxProKategorie)
                .Select(f => new SuchTreffer(nameof(Fraktion), f.Id, f.Name, f.Art ?? string.Empty, f.Aktenzeichen))
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Fraktion), "Fraktionen", treffer));
            }
        }

        // ---- Personengruppen (Name/Aktenzeichen; auch reiner Tag-Filter) ----
        if (Aktiv(nameof(Personengruppe)))
        {
            var q = db.Personengruppen.Where(g => istFuehrung || !g.IstVerschlusssache);
            if (hatText)
            {
                q = q.Where(g => g.Name.Contains(s!) || g.Aktenzeichen.Contains(s!)
                    || (g.Beschreibung != null && g.Beschreibung.Contains(s!)));
            }
            if (hatTags)
            {
                q = q.Where(g => db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Personengruppe) && z.EntitaetId == g.Id && tagIds.Contains(z.TagId)));
            }
            var treffer = await q.OrderBy(g => g.Name).Take(MaxProKategorie)
                .Select(g => new SuchTreffer(nameof(Personengruppe), g.Id, g.Name, g.Beschreibung ?? string.Empty, g.Aktenzeichen))
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Personengruppe), "Personengruppen", treffer));
            }
        }

        // Die folgenden Kategorien sind Text-Inhalte → nur bei vorhandenem Suchtext.
        // Wichtig: expliziter Join auf db.Personen (NICHT Include über die soft-delete-gefilterte
        // Pflichtnavigation), sonst greift das fragile Query-Filter-/Pflichtnavigations-Zusammenspiel.
        if (hatText && Aktiv(nameof(PersonDok)))
        {
            var treffer = await (
                from d in db.PersonDoks
                where (d.Grund != null && d.Grund.Contains(s!)) || (d.ErhalteneInformationen != null && d.ErhalteneInformationen.Contains(s!))
                join p in db.Personen on d.PersonId equals p.Id
                where (istFuehrung || !p.IstVerschlusssache)
                    && (!hatTags || db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)))
                orderby d.Zeitpunkt descending
                select new SuchTreffer(nameof(PersonDok), p.Id, p.Name,
                    (d.Grund ?? d.ErhalteneInformationen) ?? string.Empty, p.Aktenzeichen))
                .Take(MaxProKategorie)
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(PersonDok), "Doks", treffer));
            }
        }

        if (hatText && Aktiv(nameof(Quelle)))
        {
            var treffer = await (
                from quelle in db.Quellen
                where quelle.EntitaetTyp == nameof(Person)
                    && (quelle.Titel.Contains(s!) || (quelle.Beschreibung != null && quelle.Beschreibung.Contains(s!)))
                join p in db.Personen on quelle.EntitaetId equals p.Id
                where (istFuehrung || !p.IstVerschlusssache)
                    && (!hatTags || db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)))
                orderby quelle.ErstelltAm descending
                select new SuchTreffer(nameof(Quelle), p.Id, p.Name, quelle.Titel, p.Aktenzeichen))
                .Take(MaxProKategorie)
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Quelle), "Quellen", treffer));
            }
        }

        if (hatText && Aktiv(nameof(Kommentar)))
        {
            var treffer = await (
                from kommentar in db.Kommentare
                where kommentar.EntitaetTyp == nameof(Person) && kommentar.Text.Contains(s!)
                join p in db.Personen on kommentar.EntitaetId equals p.Id
                where (istFuehrung || !p.IstVerschlusssache)
                    && (!hatTags || db.TagZuordnungen.Any(z => z.EntitaetTyp == nameof(Person) && z.EntitaetId == p.Id && tagIds.Contains(z.TagId)))
                orderby kommentar.ErstelltAm descending
                select new SuchTreffer(nameof(Kommentar), p.Id, p.Name, kommentar.Text, p.Aktenzeichen))
                .Take(MaxProKategorie)
                .ToListAsync(cancellationToken);
            if (treffer.Count > 0)
            {
                gruppen.Add(new SuchErgebnisGruppe(nameof(Kommentar), "Kommentare", treffer));
            }
        }

        return gruppen;
    }

    public async Task<List<SchnellTreffer>> SchnellsucheAsync(string text, bool istFuehrung, int max = 8, CancellationToken cancellationToken = default)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return new List<SchnellTreffer>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var personen = await db.Personen
            .Where(p => (istFuehrung || !p.IstVerschlusssache) && (p.Name.Contains(s) || p.Aktenzeichen.Contains(s)))
            .OrderBy(p => p.Name).Take(max)
            .Select(p => new SchnellTreffer(nameof(Person), p.Id, p.Name, p.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var fraktionen = await db.Fraktionen
            .Where(f => (istFuehrung || !f.IstVerschlusssache) && (f.Name.Contains(s) || f.Aktenzeichen.Contains(s)))
            .OrderBy(f => f.Name).Take(max)
            .Select(f => new SchnellTreffer(nameof(Fraktion), f.Id, f.Name, f.Aktenzeichen))
            .ToListAsync(cancellationToken);
        var gruppen = await db.Personengruppen
            .Where(g => (istFuehrung || !g.IstVerschlusssache) && (g.Name.Contains(s) || g.Aktenzeichen.Contains(s)))
            .OrderBy(g => g.Name).Take(max)
            .Select(g => new SchnellTreffer(nameof(Personengruppe), g.Id, g.Name, g.Aktenzeichen))
            .ToListAsync(cancellationToken);

        // Rundlauf-Mischung, damit Personen die Trefferliste nicht verdrängen und alle Kategorien erscheinen.
        return Mischen(personen, fraktionen, gruppen).Take(max).ToList();
    }

    /// <summary>Mischt mehrere Trefferlisten im Rundlauf (P, F, G, P, F, G, …) für eine faire Verteilung.</summary>
    private static IEnumerable<SchnellTreffer> Mischen(params List<SchnellTreffer>[] listen)
    {
        for (var index = 0; ; index++)
        {
            var etwas = false;
            foreach (var liste in listen)
            {
                if (index < liste.Count)
                {
                    etwas = true;
                    yield return liste[index];
                }
            }
            if (!etwas)
            {
                yield break;
            }
        }
    }
}
