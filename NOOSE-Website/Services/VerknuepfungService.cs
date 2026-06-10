using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVerknuepfungService" />
public class VerknuepfungService(IDbContextFactory<AppDbContext> dbFactory) : IVerknuepfungService
{
    public async Task<List<VerknuepfungAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, bool istFuehrung, VerknuepfungArt? art = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Sichtbarkeit.IstAkteSichtbarAsync(db, entitaetTyp, entitaetId, istFuehrung, cancellationToken))
        {
            return new();
        }

        var query = db.Verknuepfungen
            .Where(v => (v.VonTyp == entitaetTyp && v.VonId == entitaetId)
                     || (v.NachTyp == entitaetTyp && v.NachId == entitaetId));
        if (art is not null)
        {
            query = query.Where(v => v.Art == art.Value);
        }
        var roh = await query
            .OrderByDescending(v => v.ErstelltAm)
            .ToListAsync(cancellationToken);

        // Je Verknüpfung die „andere Seite" relativ zur betrachteten Akte bestimmen.
        var paare = roh.Select(v =>
        {
            var istVon = v.VonTyp == entitaetTyp && v.VonId == entitaetId;
            return (V: v,
                    AndereTyp: istVon ? v.NachTyp : v.VonTyp,
                    AndereId: istVon ? v.NachId : v.VonId);
        }).ToList();

        // Ziele für Anzeige + Sichtbarkeit + Navigations-Href auflösen – je Typ eine Sammelabfrage, dann
        // in EINE Lookup-Map (Typ, Id) → (Bezeichnung, Verschlusssache, Href) zusammenführen.
        var ziele = new Dictionary<(string Typ, string Id), (string Bezeichnung, bool Verschluss, string? Href)>();

        var personIds = paare.Where(p => p.AndereTyp == nameof(Person)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.Personen.Where(p => personIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            ziele[(nameof(Person), x.Id)] = ($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, $"/personen/{x.Id}");
        }

        var fraktionIds = paare.Where(p => p.AndereTyp == nameof(Fraktion)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.Fraktionen.Where(f => fraktionIds.Contains(f.Id))
                     .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            ziele[(nameof(Fraktion), x.Id)] = ($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, $"/fraktionen/{x.Id}");
        }

        var gruppenIds = paare.Where(p => p.AndereTyp == nameof(Personengruppe)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.Personengruppen.Where(g => gruppenIds.Contains(g.Id))
                     .Select(g => new { g.Id, g.Name, g.Aktenzeichen, g.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            ziele[(nameof(Personengruppe), x.Id)] = ($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, $"/personengruppen/{x.Id}");
        }

        var parteiIds = paare.Where(p => p.AndereTyp == nameof(Partei)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.Parteien.Where(p => parteiIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            ziele[(nameof(Partei), x.Id)] = ($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, $"/parteien/{x.Id}");
        }

        // Operation (Phase 5b): aufgelöst mit Navigation auf die eigene Detailseite.
        var operationIds = paare.Where(p => p.AndereTyp == nameof(Operation)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.Operationen.Where(o => operationIds.Contains(o.Id))
                     .Select(o => new { o.Id, o.Titel, o.Aktenzeichen, o.IstVerschlusssache }).ToListAsync(cancellationToken))
        {
            ziele[(nameof(Operation), x.Id)] = ($"{x.Titel} ({x.Aktenzeichen})", x.IstVerschlusssache, $"/operationen/{x.Id}");
        }

        // Agent: kein Verschlusssache-Konzept und keine eigene Detailseite (Href null) – nur Codename.
        var agentIds = paare.Where(p => p.AndereTyp == nameof(Agent)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.Users.Where(u => agentIds.Contains(u.Id))
                     .Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken))
        {
            ziele[(nameof(Agent), x.Id)] = (string.IsNullOrWhiteSpace(x.Codename) ? "(unbenannter Agent)" : x.Codename, false, null);
        }

        // Personen-Dok: erbt Sichtbarkeit von seiner Person (Join auf Personen → Soft-Delete/Verschlusssache
        // greifen automatisch); Navigation auf die Personen-Akte, Doks-Tab.
        var dokIds = paare.Where(p => p.AndereTyp == nameof(PersonDok)).Select(p => p.AndereId).Distinct().ToList();
        foreach (var x in await db.PersonDoks.Where(d => dokIds.Contains(d.Id))
                     .Join(db.Personen, d => d.PersonId, p => p.Id,
                           (d, p) => new { d.Id, d.Zeitpunkt, PersonId = p.Id, PersonName = p.Name, p.IstVerschlusssache })
                     .ToListAsync(cancellationToken))
        {
            ziele[(nameof(PersonDok), x.Id)] = ($"Dok – {x.PersonName} ({x.Zeitpunkt.ToLocalTime():dd.MM.yyyy})", x.IstVerschlusssache, $"/personen/{x.PersonId}?tab=doks");
        }

        var bekannteTypen = new[]
        {
            nameof(Person), nameof(Fraktion), nameof(Personengruppe), nameof(Partei),
            nameof(Operation), nameof(Agent), nameof(PersonDok),
        };
        var ergebnis = new List<VerknuepfungAnzeige>();
        foreach (var p in paare)
        {
            if (ziele.TryGetValue((p.AndereTyp, p.AndereId), out var info))
            {
                // Verschlusssache nur für die Führung sichtbar.
                if (info.Verschluss && !istFuehrung)
                {
                    continue;
                }
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label, info.Bezeichnung, p.V.Automatisch, info.Href));
            }
            else if (bekannteTypen.Contains(p.AndereTyp))
            {
                // Bekannter Aktentyp, aber nicht aufgelöst → Ziel im Papierkorb/unbekannt → ausblenden.
            }
            else
            {
                // Unbekannter Aktentyp → vorerst Rohbezeichnung ohne Navigationsziel.
                ergebnis.Add(new VerknuepfungAnzeige(p.V.Id, p.AndereTyp, p.AndereId, p.V.Label, p.AndereId, p.V.Automatisch));
            }
        }
        return ergebnis;
    }

    public async Task ErstellenAsync(string vonTyp, string vonId, string nachTyp, string nachId, string? label, ClaimsPrincipal handelnder, VerknuepfungArt art = VerknuepfungArt.Standard, CancellationToken cancellationToken = default)
    {
        if (vonTyp == nachTyp && vonId == nachId)
        {
            throw new InvalidOperationException("Eine Akte kann nicht mit sich selbst verknüpft werden.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Doppelte (aktive) Verknüpfung derselben Art verhindern – in beiden Richtungen. Soft-gelöschte
        // (Papierkorb) sind durch den globalen Filter ausgenommen und blockieren das Neuanlegen nicht.
        var existiert = await db.Verknuepfungen.AnyAsync(v => v.Art == art
            && ((v.VonTyp == vonTyp && v.VonId == vonId && v.NachTyp == nachTyp && v.NachId == nachId)
             || (v.VonTyp == nachTyp && v.VonId == nachId && v.NachTyp == vonTyp && v.NachId == vonId)),
            cancellationToken);
        if (existiert)
        {
            throw new InvalidOperationException("Diese Verknüpfung besteht bereits.");
        }

        db.Verknuepfungen.Add(new Verknuepfung
        {
            VonTyp = vonTyp,
            VonId = vonId,
            NachTyp = nachTyp,
            NachId = nachId,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Art = art,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EntfernenAsync(string verknuepfungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var v = await db.Verknuepfungen.FirstOrDefaultAsync(x => x.Id == verknuepfungId, cancellationToken);
        if (v is null)
        {
            return;
        }
        db.Verknuepfungen.Remove(v); // Soft-Delete via Interceptor
        await db.SaveChangesAsync(cancellationToken);
    }
}
