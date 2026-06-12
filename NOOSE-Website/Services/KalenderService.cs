using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Termine;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Kalender;

namespace NOOSE_Website.Services;

/// <summary>
/// Aggregiert die Kalender-Einträge eines Zeitfensters. Wie <see cref="ZeitstrahlService"/>: alle Abfragen
/// laufen sequenziell auf EINEM kurzlebigen Context (DbContext ist nicht thread-safe), mit flachen
/// <c>WHERE</c>-Filtern (kein SelectMany/CROSS APPLY – Pomelo/MySQL). Jede Quelle ist auf das Fenster begrenzt
/// und je Quelle gedeckelt (Schutz vor Riesen-Ergebnissen). UTC wird erst beim Bauen der DTOs in lokale
/// RP-Zeit umgerechnet.
/// </summary>
public class KalenderService(IDbContextFactory<AppDbContext> dbFactory) : IKalenderService
{
    private const int ProQuelleMax = 500;

    public async Task<IReadOnlyList<KalenderEintrag>> GetEintraegeAsync(
        DateTime vonUtc, DateTime bisUtc, ClaimsPrincipal betrachter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var darfVs = betrachter.DarfVerschlusssacheLesen();
        var darfAlleTf = betrachter.DarfAlleTaskforcesSehen();
        var meId = betrachter.GetAgentId();

        var eintraege = new List<KalenderEintrag>();

        // ---- 1) Termine (eigene Akte) – Sichtbarkeit wie Aufgabe (eingeschränkt) ----
        foreach (var t in await db.Termine.NurSichtbare(db, darfVs, meId)
            .Where(t => t.Beginn <= bisUtc && (t.Ende ?? t.Beginn) >= vonUtc)
            .OrderBy(t => t.Beginn).Take(ProQuelleMax)
            .Select(t => new { t.Id, t.Titel, t.Beginn, t.Ende, t.Ganztaegig, t.Status })
            .ToListAsync(cancellationToken))
        {
            eintraege.Add(new KalenderEintrag($"tm:{t.Id}", t.Titel, Lokal(t.Beginn), LokalOpt(t.Ende),
                t.Ganztaegig, KalenderQuelle.Termin, $"/kalender/{t.Id}", TerminStatusAnzeige.IstHinfaellig(t.Status)));
        }

        // ---- 2) Operationen (Verschlusssache-gefiltert) ----
        foreach (var o in await db.Operationen
            .Where(o => (darfVs || !o.IstVerschlusssache)
                && o.Beginn != null && o.Beginn <= bisUtc && (o.Ende ?? o.Beginn) >= vonUtc)
            .OrderBy(o => o.Beginn).Take(ProQuelleMax)
            .Select(o => new { o.Id, o.Titel, o.Beginn, o.Ende, o.Status })
            .ToListAsync(cancellationToken))
        {
            eintraege.Add(new KalenderEintrag($"op:{o.Id}", o.Titel, Lokal(o.Beginn!.Value), LokalOpt(o.Ende),
                false, KalenderQuelle.Operation, $"/operationen/{o.Id}", o.Status == OperationStatus.Abgebrochen));
        }

        // ---- 3) Überwachungsfenster (Observation) – VS erbt von der Eltern-Person (INNER JOIN über Pflicht-Nav) ----
        foreach (var ob in await db.Observationen
            .Where(ob => (darfVs || !ob.Person!.IstVerschlusssache)
                && ob.Beginn <= bisUtc && (ob.Ende ?? ob.Beginn) >= vonUtc)
            .OrderBy(ob => ob.Beginn).Take(ProQuelleMax)
            .Select(ob => new { ob.Id, ob.Ort, ob.Beginn, ob.Ende, ob.PersonId })
            .ToListAsync(cancellationToken))
        {
            var titel = string.IsNullOrWhiteSpace(ob.Ort) ? "Observation" : $"Observation – {ob.Ort}";
            eintraege.Add(new KalenderEintrag($"ob:{ob.Id}", titel, Lokal(ob.Beginn), LokalOpt(ob.Ende),
                false, KalenderQuelle.Observation, $"/personen/{ob.PersonId}"));
        }

        // ---- 4) Fällige Aufgaben – Sichtbarkeit wie Team-Board (eingeschränkt) ----
        foreach (var a in await db.Aufgaben.NurSichtbare(db, darfVs, meId)
            .Where(a => a.Faelligkeit != null && a.Faelligkeit >= vonUtc && a.Faelligkeit <= bisUtc)
            .OrderBy(a => a.Faelligkeit).Take(ProQuelleMax)
            .Select(a => new { a.Id, a.Titel, a.Faelligkeit, a.Status })
            .ToListAsync(cancellationToken))
        {
            eintraege.Add(new KalenderEintrag($"auf:{a.Id}", a.Titel, Lokal(a.Faelligkeit!.Value), null,
                false, KalenderQuelle.Aufgabe, $"/aufgaben/{a.Id}", AufgabeStatusAnzeige.IstAbgeschlossen(a.Status)));
        }

        // ---- 5) Offene Wiedervorlagen – Eltern-Akte sichtbarkeitsgeprüft auflösen ----
        var wvs = await db.Wiedervorlagen
            .Where(w => !w.Erledigt && w.FaelligAm >= vonUtc && w.FaelligAm <= bisUtc)
            .OrderBy(w => w.FaelligAm).Take(ProQuelleMax)
            .Select(w => new { w.Id, w.Notiz, w.FaelligAm, w.EntitaetTyp, w.EntitaetId })
            .ToListAsync(cancellationToken);
        if (wvs.Count > 0)
        {
            var refs = wvs.Select(w => (w.EntitaetTyp, w.EntitaetId)).Distinct().ToList();
            var map = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken, darfAlleTf, meId);
            foreach (var w in wvs)
            {
                // Nicht auflösbar (Papierkorb/unbekannt) oder Verschlusssache für Nicht-Führung → kein Eintrag (kein Leak).
                if (!map.TryGetValue((w.EntitaetTyp, w.EntitaetId), out var eltern) || (eltern.Verschluss && !darfVs))
                {
                    continue;
                }
                var titel = string.IsNullOrWhiteSpace(w.Notiz)
                    ? $"Wiedervorlage: {eltern.Anzeige}"
                    : $"Wiedervorlage: {eltern.Anzeige} · {w.Notiz}";
                eintraege.Add(new KalenderEintrag($"wv:{w.Id}", titel, Lokal(w.FaelligAm), null,
                    false, KalenderQuelle.Wiedervorlage, eltern.Href));
            }
        }

        // ---- 6) Fraktions-Aktivitäten – VS erbt von der Eltern-Fraktion ----
        foreach (var fa in await db.FraktionAktivitaeten
            .Where(fa => (darfVs || !fa.Fraktion!.IstVerschlusssache)
                && fa.Zeitpunkt >= vonUtc && fa.Zeitpunkt <= bisUtc)
            .OrderBy(fa => fa.Zeitpunkt).Take(ProQuelleMax)
            .Select(fa => new { fa.Id, fa.Titel, fa.Art, fa.Zeitpunkt, fa.FraktionId })
            .ToListAsync(cancellationToken))
        {
            var titel = string.IsNullOrWhiteSpace(fa.Art) ? fa.Titel : $"{fa.Titel} ({fa.Art})";
            eintraege.Add(new KalenderEintrag($"fa:{fa.Id}", titel, Lokal(fa.Zeitpunkt), null,
                false, KalenderQuelle.FraktionAktivitaet, $"/fraktionen/{fa.FraktionId}"));
        }

        return eintraege;
    }

    // UTC (in der DB als Unspecified/Utc abgelegt) → lokale Wandzeit ohne Kind (FullCalendar liest naiv-lokal).
    private static DateTime Lokal(DateTime utc)
        => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Unspecified);

    private static DateTime? LokalOpt(DateTime? utc) => utc is { } u ? Lokal(u) : null;
}
