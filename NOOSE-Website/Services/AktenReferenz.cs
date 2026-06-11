using Microsoft.EntityFrameworkCore;
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
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Auflösung von Objekt-Verweisen (Typ + Id) zu Anzeigename, Verschlusssache-Flag und Navigations-Href.
/// Spiegelt die Typ→(Bezeichnung, VS, Href)-Auflösung aus <see cref="VerknuepfungService"/>, ergänzt um
/// <see cref="Quelle"/> und <see cref="Agent"/>; dient den @-Mentions (siehe MentionService). Liefert das ECHTE
/// Verschlusssache-Flag – das Ausblenden für Nicht-Führung übernimmt der Aufrufer, damit sensible Namen
/// serverseitig bleiben (Blazor Server serialisiert verborgene Werte nie zum Client).
/// </summary>
public static class AktenReferenz
{
    public readonly record struct Aufloesung(string Anzeige, bool Verschluss, string? Href);

    /// <summary>Löst alle angegebenen (Typ, Id)-Verweise in einer Sammelabfrage je Typ auf.
    /// Taskforces werden NUR aufgelöst, wenn der Betrachter sie sehen darf (<paramref name="darfAlleTaskforces"/>
    /// = Führung/Admin/Aufsicht, sonst muss er via <paramref name="meId"/> zugeteilt sein); nicht sichtbare
    /// Taskforce-Verweise fehlen im Ergebnis (Aufrufer zeigen sie dann als „(nicht verfügbar)"/gar nicht).
    /// Hintergrund-Fan-out-Dienste, die für viele Empfänger EINMAL auflösen, übergeben
    /// (darfAlleTaskforces: true, meId: null) und filtern die Zustellung separat pro Empfänger.</summary>
    // Standard (darfAlleTaskforces: true) = ALLE Taskforces auflösen (bisheriges Verhalten). Betrachter-bezogene
    // Aufrufer MÜSSEN den echten Kontext (darfAlleTaskforces=DarfAlleTaskforcesSehen, meId) übergeben, damit fremde
    // Taskforces gar nicht erst aufgelöst werden. Hintergrund-Fan-out (viele Empfänger) bleibt beim Standard und
    // filtert pro Empfänger separat (IstAkteSichtbarAsync mit Empfänger-Id).
    public static async Task<Dictionary<(string Typ, string Id), Aufloesung>> AufloesenAsync(
        AppDbContext db, IReadOnlyCollection<(string Typ, string Id)> refs, CancellationToken ct = default,
        bool darfAlleTaskforces = true, string? meId = null)
    {
        var map = new Dictionary<(string, string), Aufloesung>();
        if (refs.Count == 0)
        {
            return map;
        }

        await ResolveAktenAsync(db, refs, map, darfAlleTaskforces, meId, ct);

        // Quellen: Anzeige = Titel; Verschlusssache + Route von der Eltern-Akte abgeleitet.
        var quelleIds = refs.Where(r => r.Typ == nameof(Quelle)).Select(r => r.Id).Distinct().ToList();
        if (quelleIds.Count > 0)
        {
            var quellen = await db.Quellen.Where(q => quelleIds.Contains(q.Id))
                .Select(q => new { q.Id, q.Titel, q.Typ, q.Url, q.EntitaetTyp, q.EntitaetId })
                .ToListAsync(ct);
            // Eltern-Akten der Quellen auflösen (für VS + Route), falls nicht ohnehin schon aufgelöst.
            var elternRefs = quellen.Select(q => (q.EntitaetTyp, q.EntitaetId)).Distinct().ToList();
            await ResolveAktenAsync(db, elternRefs, map, darfAlleTaskforces, meId, ct);
            foreach (var q in quellen)
            {
                map.TryGetValue((q.EntitaetTyp, q.EntitaetId), out var eltern);
                var href = q.Typ switch
                {
                    QuelleTyp.Upload => $"/dateien/quellen/{q.Id}",
                    QuelleTyp.Link => string.IsNullOrWhiteSpace(q.Url) ? null : q.Url,
                    _ => eltern.Href is { } h ? $"{h}?tab=quellen" : null,
                };
                map[(nameof(Quelle), q.Id)] = new Aufloesung(
                    string.IsNullOrWhiteSpace(q.Titel) ? "Quelle" : q.Titel,
                    eltern.Verschluss, href);
            }
        }

        return map;
    }

    private static async Task ResolveAktenAsync(
        AppDbContext db, IReadOnlyCollection<(string Typ, string Id)> refs,
        Dictionary<(string, string), Aufloesung> map, bool darfAlleTaskforces, string? meId, CancellationToken ct)
    {
        List<string> OffeneIds(string typ) => refs
            .Where(r => r.Typ == typ && !map.ContainsKey((typ, r.Id)))
            .Select(r => r.Id).Distinct().ToList();

        var personIds = OffeneIds(nameof(Person));
        if (personIds.Count > 0)
        {
            foreach (var x in await db.Personen.Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Person), x.Id)] = new($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, SuchNavigation.Route(nameof(Person), x.Id));
            }
        }

        var fraktionIds = OffeneIds(nameof(Fraktion));
        if (fraktionIds.Count > 0)
        {
            foreach (var x in await db.Fraktionen.Where(f => fraktionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Fraktion), x.Id)] = new($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, SuchNavigation.Route(nameof(Fraktion), x.Id));
            }
        }

        var gruppenIds = OffeneIds(nameof(Personengruppe));
        if (gruppenIds.Count > 0)
        {
            foreach (var x in await db.Personengruppen.Where(g => gruppenIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.Aktenzeichen, g.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Personengruppe), x.Id)] = new($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, SuchNavigation.Route(nameof(Personengruppe), x.Id));
            }
        }

        var parteiIds = OffeneIds(nameof(Partei));
        if (parteiIds.Count > 0)
        {
            foreach (var x in await db.Parteien.Where(p => parteiIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Partei), x.Id)] = new($"{x.Name} ({x.Aktenzeichen})", x.IstVerschlusssache, SuchNavigation.Route(nameof(Partei), x.Id));
            }
        }

        var operationIds = OffeneIds(nameof(Operation));
        if (operationIds.Count > 0)
        {
            foreach (var x in await db.Operationen.Where(o => operationIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Titel, o.Aktenzeichen, o.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Operation), x.Id)] = new($"{x.Titel} ({x.Aktenzeichen})", x.IstVerschlusssache, SuchNavigation.Route(nameof(Operation), x.Id));
            }
        }

        var taskforceIds = OffeneIds(nameof(Taskforce));
        if (taskforceIds.Count > 0)
        {
            // Nur die für den Betrachter sichtbaren Taskforces auflösen (zugeteilt oder darf alle sehen);
            // die übrigen bleiben unaufgelöst → Aufrufer zeigen sie als „(nicht verfügbar)"/gar nicht.
            var sichtbar = await TaskforceSichtbarkeit.SichtbareIdsAsync(db, taskforceIds, darfAlleTaskforces, meId, ct);
            if (sichtbar.Count > 0)
            {
                foreach (var x in await db.Taskforces.Where(t => sichtbar.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name, t.Aktenzeichen }).ToListAsync(ct))
                {
                    // Verschluss bewusst false: Die Mitgliedschaft hat die Sichtbarkeit bereits entschieden (nicht
                    // sichtbare Taskforces sind gar nicht in `sichtbar`). So verbergen nachgelagerte VS-Prüfungen
                    // der Aufrufer einem zugeteilten Mitglied NICHT fälschlich den Namen seiner VS-Taskforce.
                    map[(nameof(Taskforce), x.Id)] = new($"{x.Name} ({x.Aktenzeichen})", false, SuchNavigation.Route(nameof(Taskforce), x.Id));
                }
            }
        }

        var vorgangIds = OffeneIds(nameof(Vorgang));
        if (vorgangIds.Count > 0)
        {
            foreach (var x in await db.Vorgaenge.Where(v => vorgangIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Titel, v.Aktenzeichen, v.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Vorgang), x.Id)] = new($"{x.Titel} ({x.Aktenzeichen})", x.IstVerschlusssache, SuchNavigation.Route(nameof(Vorgang), x.Id));
            }
        }

        // Aufgabe: kein Verschlusssache-Konzept (Team-Board), ABER „eingeschränkte" Aufgaben sind nur für
        // Beteiligte (Ersteller/Zugeteilte) bzw. die Aufsicht sichtbar. darfAlleTaskforces trägt hier denselben
        // „Aufsicht darf alles"-Wert (DarfAlleTaskforcesSehen == DarfVerschlusssacheLesen); nicht sichtbare
        // Aufgaben bleiben unaufgelöst → Aufrufer zeigen sie als „(nicht verfügbar)"/gar nicht. Verschluss fest false.
        var aufgabeIds = OffeneIds(nameof(Aufgabe));
        if (aufgabeIds.Count > 0)
        {
            var sichtbar = await AufgabeSichtbarkeit.SichtbareIdsAsync(db, aufgabeIds, darfAlleTaskforces, meId, ct);
            if (sichtbar.Count > 0)
            {
                foreach (var x in await db.Aufgaben.Where(a => sichtbar.Contains(a.Id))
                    .Select(a => new { a.Id, a.Titel, a.Aktenzeichen }).ToListAsync(ct))
                {
                    map[(nameof(Aufgabe), x.Id)] = new($"{x.Titel} ({x.Aktenzeichen})", false, SuchNavigation.Route(nameof(Aufgabe), x.Id));
                }
            }
        }

        // Bibliotheks-Dokument: Anzeige = Titel, echtes Verschlusssache-Flag, Route auf den Viewer.
        var dokumentIds = OffeneIds(nameof(Dokument));
        if (dokumentIds.Count > 0)
        {
            foreach (var x in await db.Dokumente.Where(d => dokumentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Titel, d.IstVerschlusssache }).ToListAsync(ct))
            {
                map[(nameof(Dokument), x.Id)] = new(
                    string.IsNullOrWhiteSpace(x.Titel) ? "Dokument" : x.Titel,
                    x.IstVerschlusssache, SuchNavigation.Route(nameof(Dokument), x.Id));
            }
        }

        // Agent: kein Verschlusssache-Konzept; Verweis auf die Personalakte (/personal/{id}, für jeden aktiven
        // Agenten zugänglich) – nur der Codename als Anzeigename (Klarname bleibt verborgen).
        var agentIds = OffeneIds(nameof(Agent));
        if (agentIds.Count > 0)
        {
            foreach (var x in await db.Users.Where(u => agentIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename }).ToListAsync(ct))
            {
                map[(nameof(Agent), x.Id)] = new(string.IsNullOrWhiteSpace(x.Codename) ? "(unbenannter Agent)" : x.Codename,
                    false, $"/personal/{x.Id}");
            }
        }
    }
}
