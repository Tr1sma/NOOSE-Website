using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IGraphService" />
public class GraphService(IDbContextFactory<AppDbContext> dbFactory) : IGraphService
{
    /// <summary>Obergrenze der Knoten im Gesamtgraph (ohne Fokus). Darüber wird auf die Knoten mit dem
    /// höchsten Grad reduziert und das Ergebnis als „abgeschnitten" markiert.</summary>
    private const int MaxKnoten = 250;

    /// <summary>Sicherheitsnetz der Pfadsuche: maximale Hop-Tiefe und maximale Zahl besuchter Knoten.</summary>
    private const int MaxPfadTiefe = 12;
    private const int MaxBesucht = 8000;

    /// <summary>Roh-Kante zwischen zwei Graph-Schlüsseln, vor Sichtbarkeits-/Typ-Filterung.</summary>
    private readonly record struct RohKante(string Von, string Nach, string? Label, VerknuepfungArt Art, bool Automatisch);

    public async Task<GraphDaten> GetGraphAsync(GraphAnfrage anfrage, ClaimsPrincipal betrachter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var istFuehrung = betrachter.IstFuehrung();
        var meId = betrachter.GetAgentId();

        var rohKanten = await LadeRohKantenAsync(db, anfrage.ArtFilter, cancellationToken);

        // Alle vorkommenden Knoten einsammeln (+ den Fokusknoten, falls er kantenlos ist).
        var keys = new HashSet<string>();
        foreach (var k in rohKanten)
        {
            keys.Add(k.Von);
            keys.Add(k.Nach);
        }
        if (anfrage.FokusTyp is not null && anfrage.FokusId is not null)
        {
            keys.Add($"{anfrage.FokusTyp}:{anfrage.FokusId}");
        }

        // Knoten sichtbarkeitsgeprüft auflösen; nicht sichtbare fallen hier bereits weg.
        var knoten = await LoeseKnotenAsync(db, keys, istFuehrung, meId, cancellationToken);

        // Typ-Filter (falls gesetzt) auf die aufgelösten Knoten anwenden.
        if (anfrage.TypFilter is { Count: > 0 })
        {
            var erlaubt = anfrage.TypFilter.ToHashSet();
            foreach (var key in knoten.Keys.ToList())
            {
                if (!erlaubt.Contains(knoten[key].Typ))
                {
                    knoten.Remove(key);
                }
            }
        }

        // Kanten auf Paare sichtbarer/erlaubter Knoten reduzieren (Selbstkanten raus).
        var kanten = rohKanten
            .Where(k => k.Von != k.Nach && knoten.ContainsKey(k.Von) && knoten.ContainsKey(k.Nach))
            .ToList();

        var abgeschnitten = false;
        HashSet<string> behalten;

        if (anfrage.FokusTyp is not null && anfrage.FokusId is not null)
        {
            var fokusKey = $"{anfrage.FokusTyp}:{anfrage.FokusId}";
            if (!knoten.ContainsKey(fokusKey))
            {
                // Fokusakte nicht sichtbar/vorhanden → leerer Graph.
                return new GraphDaten(Array.Empty<GraphKnoten>(), Array.Empty<GraphKante>(), false);
            }
            behalten = Umkreis(fokusKey, kanten, Math.Clamp(anfrage.Tiefe, 1, 3));
        }
        else
        {
            behalten = knoten.Keys.ToHashSet();
            if (behalten.Count > MaxKnoten)
            {
                var grad = GradZaehlen(kanten);
                behalten = behalten
                    .OrderByDescending(k => grad.TryGetValue(k, out var g) ? g : 0)
                    .Take(MaxKnoten)
                    .ToHashSet();
                abgeschnitten = true;
            }
        }

        var finaleKanten = kanten
            .Where(k => behalten.Contains(k.Von) && behalten.Contains(k.Nach))
            .ToList();
        var gradFinal = GradZaehlen(finaleKanten);

        var knotenListe = behalten
            .Where(knoten.ContainsKey)
            .Select(k => knoten[k] with { Grad = gradFinal.TryGetValue(k, out var g) ? g : 0 })
            .ToList();
        var kantenListe = finaleKanten
            .Select(k => new GraphKante(k.Von, k.Nach, k.Label, k.Art, k.Automatisch))
            .ToList();

        return new GraphDaten(knotenListe, kantenListe, abgeschnitten);
    }

    public async Task<PfadErgebnis> FindePfadAsync(string vonTyp, string vonId, string nachTyp, string nachId, ClaimsPrincipal betrachter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var istFuehrung = betrachter.IstFuehrung();
        var meId = betrachter.GetAgentId();

        var vonKey = $"{vonTyp}:{vonId}";
        var nachKey = $"{nachTyp}:{nachId}";

        var rohKanten = await LadeRohKantenAsync(db, null, cancellationToken);
        var keys = new HashSet<string> { vonKey, nachKey };
        foreach (var k in rohKanten)
        {
            keys.Add(k.Von);
            keys.Add(k.Nach);
        }
        var knoten = await LoeseKnotenAsync(db, keys, istFuehrung, meId, cancellationToken);

        // Start oder Ziel nicht sichtbar/vorhanden → kein Pfad.
        if (!knoten.ContainsKey(vonKey) || !knoten.ContainsKey(nachKey))
        {
            return new PfadErgebnis(false, Array.Empty<GraphKnoten>(), Array.Empty<GraphKante>());
        }
        if (vonKey == nachKey)
        {
            return new PfadErgebnis(true, new[] { knoten[vonKey] }, Array.Empty<GraphKante>());
        }

        // Adjazenz nur unter sichtbaren Knoten aufbauen.
        var adj = new Dictionary<string, List<RohKante>>();
        void Verbinde(string a, RohKante k)
        {
            if (!adj.TryGetValue(a, out var liste))
            {
                liste = new();
                adj[a] = liste;
            }
            liste.Add(k);
        }
        foreach (var k in rohKanten)
        {
            if (k.Von == k.Nach || !knoten.ContainsKey(k.Von) || !knoten.ContainsKey(k.Nach))
            {
                continue;
            }
            Verbinde(k.Von, k);
            Verbinde(k.Nach, k);
        }

        // Breitensuche mit Vorgänger-Tabelle (Knoten → benutzte Kante).
        var vorgaenger = new Dictionary<string, RohKante>();
        var tiefe = new Dictionary<string, int> { [vonKey] = 0 };
        var schlange = new Queue<string>();
        schlange.Enqueue(vonKey);
        var besucht = 0;
        var gefunden = false;

        while (schlange.Count > 0)
        {
            var aktuell = schlange.Dequeue();
            if (aktuell == nachKey)
            {
                gefunden = true;
                break;
            }
            if (++besucht > MaxBesucht || tiefe[aktuell] >= MaxPfadTiefe || !adj.TryGetValue(aktuell, out var nachbarn))
            {
                continue;
            }
            foreach (var kante in nachbarn)
            {
                var anderer = kante.Von == aktuell ? kante.Nach : kante.Von;
                if (tiefe.ContainsKey(anderer))
                {
                    continue;
                }
                tiefe[anderer] = tiefe[aktuell] + 1;
                vorgaenger[anderer] = kante;
                schlange.Enqueue(anderer);
            }
        }

        if (!gefunden)
        {
            return new PfadErgebnis(false, Array.Empty<GraphKnoten>(), Array.Empty<GraphKante>());
        }

        // Pfad vom Ziel zurück zum Start rekonstruieren und umdrehen.
        var knotenPfad = new List<string> { nachKey };
        var kantenPfad = new List<RohKante>();
        var cursor = nachKey;
        while (cursor != vonKey)
        {
            var kante = vorgaenger[cursor];
            kantenPfad.Add(kante);
            cursor = kante.Von == cursor ? kante.Nach : kante.Von;
            knotenPfad.Add(cursor);
        }
        knotenPfad.Reverse();
        kantenPfad.Reverse();

        return new PfadErgebnis(
            true,
            knotenPfad.Select(k => knoten[k]).ToList(),
            kantenPfad.Select(k => new GraphKante(k.Von, k.Nach, k.Label, k.Art, k.Automatisch)).ToList());
    }

    // ---- Kanten laden (beide Quellen, optional auf eine Art gefiltert) ----

    private static async Task<List<RohKante>> LadeRohKantenAsync(AppDbContext db, VerknuepfungArt? artFilter, CancellationToken cancellationToken)
    {
        var vq = db.Verknuepfungen.AsQueryable();
        if (artFilter is not null)
        {
            vq = vq.Where(v => v.Art == artFilter.Value);
        }
        var verkn = await vq
            .Select(v => new { v.VonTyp, v.VonId, v.NachTyp, v.NachId, v.Label, v.Art, v.Automatisch })
            .ToListAsync(cancellationToken);

        var kanten = new List<RohKante>(verkn.Count);
        foreach (var v in verkn)
        {
            kanten.Add(new RohKante($"{v.VonTyp}:{v.VonId}", $"{v.NachTyp}:{v.NachId}", v.Label, v.Art, v.Automatisch));
        }

        // Person-zu-Person-Beziehungen auf eine Verknüpfungs-Art mappen (Feind→Konflikt, Verbündeter→Bündnis).
        var bez = await db.PersonBeziehungen
            .Select(b => new { b.PersonAId, b.PersonBId, b.Typ })
            .ToListAsync(cancellationToken);
        foreach (var b in bez)
        {
            var art = b.Typ switch
            {
                BeziehungsTyp.Feind => VerknuepfungArt.Konflikt,
                BeziehungsTyp.Verbuendeter => VerknuepfungArt.Buendnis,
                _ => VerknuepfungArt.Standard,
            };
            if (artFilter is not null && art != artFilter.Value)
            {
                continue;
            }
            kanten.Add(new RohKante(
                $"{nameof(Person)}:{b.PersonAId}",
                $"{nameof(Person)}:{b.PersonBId}",
                BeziehungsTypAnzeige.Name(b.Typ),
                art,
                false));
        }
        return kanten;
    }

    // ---- Knoten-Auflösung (verallgemeinert aus VerknuepfungService.GetFuerAkteAsync) ----
    // Je Aktentyp eine Sammelabfrage; nicht sichtbare Akten (Verschlusssache/fremde Taskforce/Papierkorb)
    // werden gar nicht erst aufgenommen, sodass die anhängenden Kanten später automatisch wegfallen.

    private static async Task<Dictionary<string, GraphKnoten>> LoeseKnotenAsync(
        AppDbContext db, IEnumerable<string> keys, bool istFuehrung, string? meId, CancellationToken cancellationToken)
    {
        // Schlüssel nach Typ gruppieren.
        var nachTyp = new Dictionary<string, HashSet<string>>();
        foreach (var key in keys)
        {
            var idx = key.IndexOf(':');
            if (idx <= 0 || idx >= key.Length - 1)
            {
                continue;
            }
            var typ = key[..idx];
            var id = key[(idx + 1)..];
            if (!nachTyp.TryGetValue(typ, out var set))
            {
                set = new();
                nachTyp[typ] = set;
            }
            set.Add(id);
        }

        var result = new Dictionary<string, GraphKnoten>();
        GraphKnoten Mk(string typ, string id, string bez, string? unter, string? href, int einstufung, bool vs)
            => new($"{typ}:{id}", typ, bez, unter, href, einstufung, vs, null, 0);

        List<string> Ids(string typ) => nachTyp.TryGetValue(typ, out var s) ? s.ToList() : new();

        // ---- Person (mit Einstufungs-Farbe + Foto-Thumbnail) ----
        var personIds = Ids(nameof(Person));
        if (personIds.Count > 0)
        {
            var rows = await db.Personen.Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache, p.Einstufung })
                .ToListAsync(cancellationToken);
            foreach (var x in rows)
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Person)}:{x.Id}"] = Mk(nameof(Person), x.Id, x.Name, x.Aktenzeichen, $"/personen/{x.Id}", (int)x.Einstufung, x.IstVerschlusssache);
            }

            var sichtbarePers = rows.Where(r => istFuehrung || !r.IstVerschlusssache).Select(r => r.Id).ToList();
            if (sichtbarePers.Count > 0)
            {
                var fotos = await db.PersonFotos.Where(f => sichtbarePers.Contains(f.PersonId))
                    .Select(f => new { f.Id, f.PersonId, f.ErstelltAm })
                    .ToListAsync(cancellationToken);
                foreach (var grp in fotos.GroupBy(f => f.PersonId))
                {
                    var erstes = grp.OrderBy(f => f.ErstelltAm).First();
                    var key = $"{nameof(Person)}:{grp.Key}";
                    if (result.TryGetValue(key, out var kn))
                    {
                        result[key] = kn with { FotoUrl = $"/dateien/personen/foto/{erstes.Id}" };
                    }
                }
            }
        }

        // ---- Fraktion ----
        var fraktionIds = Ids(nameof(Fraktion));
        if (fraktionIds.Count > 0)
        {
            foreach (var x in await db.Fraktionen.Where(f => fraktionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.IstVerschlusssache, f.Einstufung }).ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Fraktion)}:{x.Id}"] = Mk(nameof(Fraktion), x.Id, x.Name, x.Aktenzeichen, $"/fraktionen/{x.Id}", (int)x.Einstufung, x.IstVerschlusssache);
            }
        }

        // ---- Personengruppe ----
        var gruppenIds = Ids(nameof(Personengruppe));
        if (gruppenIds.Count > 0)
        {
            foreach (var x in await db.Personengruppen.Where(g => gruppenIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.Aktenzeichen, g.IstVerschlusssache, g.Einstufung }).ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Personengruppe)}:{x.Id}"] = Mk(nameof(Personengruppe), x.Id, x.Name, x.Aktenzeichen, $"/personengruppen/{x.Id}", (int)x.Einstufung, x.IstVerschlusssache);
            }
        }

        // ---- Partei ----
        var parteiIds = Ids(nameof(Partei));
        if (parteiIds.Count > 0)
        {
            foreach (var x in await db.Parteien.Where(p => parteiIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache, p.Einstufung }).ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Partei)}:{x.Id}"] = Mk(nameof(Partei), x.Id, x.Name, x.Aktenzeichen, $"/parteien/{x.Id}", (int)x.Einstufung, x.IstVerschlusssache);
            }
        }

        // ---- Operation ----
        var operationIds = Ids(nameof(Operation));
        if (operationIds.Count > 0)
        {
            foreach (var x in await db.Operationen.Where(o => operationIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Titel, o.Aktenzeichen, o.IstVerschlusssache, o.Einstufung }).ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Operation)}:{x.Id}"] = Mk(nameof(Operation), x.Id, x.Titel, x.Aktenzeichen, $"/operationen/{x.Id}", (int)x.Einstufung, x.IstVerschlusssache);
            }
        }

        // ---- Vorgang ----
        var vorgangIds = Ids(nameof(Vorgang));
        if (vorgangIds.Count > 0)
        {
            foreach (var x in await db.Vorgaenge.Where(v => vorgangIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Titel, v.Aktenzeichen, v.IstVerschlusssache, v.Einstufung }).ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Vorgang)}:{x.Id}"] = Mk(nameof(Vorgang), x.Id, x.Titel, x.Aktenzeichen, $"/vorgaenge/{x.Id}", (int)x.Einstufung, x.IstVerschlusssache);
            }
        }

        // ---- Taskforce (nur sichtbare auflösen) ----
        var taskforceIds = Ids(nameof(Taskforce));
        if (taskforceIds.Count > 0)
        {
            var sichtbar = await TaskforceSichtbarkeit.SichtbareIdsAsync(db, taskforceIds, istFuehrung, meId, cancellationToken);
            foreach (var x in await db.Taskforces.Where(t => sichtbar.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.Aktenzeichen, t.IstVerschlusssache }).ToListAsync(cancellationToken))
            {
                result[$"{nameof(Taskforce)}:{x.Id}"] = Mk(nameof(Taskforce), x.Id, x.Name, x.Aktenzeichen, $"/taskforces/{x.Id}", 0, x.IstVerschlusssache);
            }
        }

        // ---- Aufgabe (kein Verschlusssache-Konzept) ----
        var aufgabeIds = Ids(nameof(Aufgabe));
        if (aufgabeIds.Count > 0)
        {
            foreach (var x in await db.Aufgaben.Where(a => aufgabeIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Titel, a.Aktenzeichen }).ToListAsync(cancellationToken))
            {
                result[$"{nameof(Aufgabe)}:{x.Id}"] = Mk(nameof(Aufgabe), x.Id, x.Titel, x.Aktenzeichen, $"/aufgaben/{x.Id}", 0, false);
            }
        }

        // ---- Agent (Codename, keine eigene Detailseite) ----
        var agentIds = Ids(nameof(Agent));
        if (agentIds.Count > 0)
        {
            foreach (var x in await db.Users.Where(u => agentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken))
            {
                var name = string.IsNullOrWhiteSpace(x.Codename) ? "(unbenannter Agent)" : x.Codename;
                result[$"{nameof(Agent)}:{x.Id}"] = Mk(nameof(Agent), x.Id, name, null, null, 0, false);
            }
        }

        // ---- Gesetz (Wissensbasis, keine Verschlusssache) ----
        var gesetzIds = Ids(nameof(Gesetz));
        if (gesetzIds.Count > 0)
        {
            foreach (var x in await db.Gesetze.Where(g => gesetzIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Paragraf, g.Titel, g.Gesetzbuch }).ToListAsync(cancellationToken))
            {
                result[$"{nameof(Gesetz)}:{x.Id}"] = Mk(nameof(Gesetz), x.Id, $"{x.Paragraf} {x.Titel}", x.Gesetzbuch, $"/gesetze/{x.Id}", 0, false);
            }
        }

        // ---- Personen-Dok (erbt Sichtbarkeit der Person; Navigation auf den Doks-Tab) ----
        var dokIds = Ids(nameof(PersonDok));
        if (dokIds.Count > 0)
        {
            foreach (var x in await db.PersonDoks.Where(d => dokIds.Contains(d.Id))
                .Join(db.Personen, d => d.PersonId, p => p.Id,
                      (d, p) => new { d.Id, d.Zeitpunkt, PersonId = p.Id, PersonName = p.Name, p.IstVerschlusssache })
                .ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(PersonDok)}:{x.Id}"] = Mk(nameof(PersonDok), x.Id,
                    $"Dok – {x.PersonName}", x.Zeitpunkt.ToLocalTime().ToString("dd.MM.yyyy"),
                    $"/personen/{x.PersonId}?tab=doks", 0, x.IstVerschlusssache);
            }
        }

        // ---- Observation (erbt Sichtbarkeit der Person; Navigation auf den Überwachungs-Tab) ----
        var observationIds = Ids(nameof(Observation));
        if (observationIds.Count > 0)
        {
            foreach (var x in await db.Observationen.Where(o => observationIds.Contains(o.Id))
                .Join(db.Personen, o => o.PersonId, p => p.Id,
                      (o, p) => new { o.Id, o.Beginn, PersonId = p.Id, PersonName = p.Name, p.IstVerschlusssache })
                .ToListAsync(cancellationToken))
            {
                if (x.IstVerschlusssache && !istFuehrung)
                {
                    continue;
                }
                result[$"{nameof(Observation)}:{x.Id}"] = Mk(nameof(Observation), x.Id,
                    $"Observation – {x.PersonName}", x.Beginn.ToLocalTime().ToString("dd.MM.yyyy"),
                    $"/personen/{x.PersonId}?tab=ueberwachung", 0, x.IstVerschlusssache);
            }
        }

        return result;
    }

    // ---- Graph-Hilfen ----

    /// <summary>Knotengrad (Anzahl anliegender Kanten) je Knoten.</summary>
    private static Dictionary<string, int> GradZaehlen(IEnumerable<RohKante> kanten)
    {
        var grad = new Dictionary<string, int>();
        foreach (var k in kanten)
        {
            grad[k.Von] = grad.TryGetValue(k.Von, out var a) ? a + 1 : 1;
            grad[k.Nach] = grad.TryGetValue(k.Nach, out var b) ? b + 1 : 1;
        }
        return grad;
    }

    /// <summary>Alle Knoten im Umkreis von <paramref name="start"/> bis <paramref name="tiefe"/> Hops (inkl. Start).</summary>
    private static HashSet<string> Umkreis(string start, IEnumerable<RohKante> kanten, int tiefe)
    {
        var adj = new Dictionary<string, List<string>>();
        void Verbinde(string a, string b)
        {
            if (!adj.TryGetValue(a, out var liste))
            {
                liste = new();
                adj[a] = liste;
            }
            liste.Add(b);
        }
        foreach (var k in kanten)
        {
            Verbinde(k.Von, k.Nach);
            Verbinde(k.Nach, k.Von);
        }

        var besucht = new HashSet<string> { start };
        var rand = new List<string> { start };
        for (var hop = 0; hop < tiefe; hop++)
        {
            var naechste = new List<string>();
            foreach (var knoten in rand)
            {
                if (!adj.TryGetValue(knoten, out var nachbarn))
                {
                    continue;
                }
                foreach (var n in nachbarn)
                {
                    if (besucht.Add(n))
                    {
                        naechste.Add(n);
                    }
                }
            }
            if (naechste.Count == 0)
            {
                break;
            }
            rand = naechste;
        }
        return besucht;
    }
}
