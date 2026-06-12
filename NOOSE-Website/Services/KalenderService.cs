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
/// Stellt die Kalender-Einträge eines Zeitfensters zusammen – rein lesend, zwei Sichten
/// (<see cref="KalenderModus"/>). „Mein" = persönliche Agenda (eigene Termine + zugewiesene Aufgaben + eigene
/// Wiedervorlagen). „Behörde" = behördenweit (öffentliche Termine + Operationen + Überwachungsfenster +
/// Personen-Doks + Fraktions-Aktivitäten). Wie <see cref="ZeitstrahlService"/>: alle Abfragen sequenziell auf
/// EINEM kurzlebigen Context, flache <c>WHERE</c>-Filter (kein SelectMany/CROSS APPLY), je Quelle gedeckelt.
/// Jede Quelle behält ihre kanonische Sichtbarkeit; UTC wird erst beim DTO-Bau in lokale RP-Zeit umgerechnet.
/// </summary>
public class KalenderService(IDbContextFactory<AppDbContext> dbFactory) : IKalenderService
{
    private const int ProQuelleMax = 500;

    public async Task<IReadOnlyList<KalenderEintrag>> GetEintraegeAsync(
        DateTime vonUtc, DateTime bisUtc, ClaimsPrincipal betrachter, KalenderModus modus, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var darfVs = betrachter.DarfVerschlusssacheLesen();
        var meId = betrachter.GetAgentId();
        var eintraege = new List<KalenderEintrag>();

        if (modus == KalenderModus.Mein)
        {
            await LadeMeinAsync(db, vonUtc, bisUtc, darfVs, meId, eintraege, cancellationToken);
        }
        else
        {
            await LadeBehoerdeAsync(db, vonUtc, bisUtc, darfVs, eintraege, cancellationToken);
        }

        return eintraege;
    }

    // ---- „Mein Kalender": eigene Termine + mir zugewiesene Aufgaben + meine Wiedervorlagen ----
    private async Task LadeMeinAsync(AppDbContext db, DateTime vonUtc, DateTime bisUtc, bool darfVs, string? meId,
        List<KalenderEintrag> eintraege, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(meId))
        {
            return; // ohne Agent-Kontext keine persönliche Agenda
        }

        // Termine, an denen ich beteiligt bin (Ersteller oder Teilnehmer) – jede Stufe.
        foreach (var t in await db.Termine.NurEigene(db, meId)
            .Where(t => t.Beginn <= bisUtc && (t.Ende ?? t.Beginn) >= vonUtc)
            .OrderBy(t => t.Beginn).Take(ProQuelleMax)
            .Select(t => new { t.Id, t.Titel, t.Beginn, t.Ende, t.Ganztaegig, t.Status })
            .ToListAsync(ct))
        {
            eintraege.Add(new KalenderEintrag($"tm:{t.Id}", t.Titel, Lokal(t.Beginn), LokalOpt(t.Ende),
                t.Ganztaegig, KalenderQuelle.Termin, $"/kalender/{t.Id}", TerminStatusAnzeige.IstHinfaellig(t.Status)));
        }

        // Mir zugewiesene (oder selbst erstellte) Aufgaben mit Fälligkeit.
        foreach (var a in await db.Aufgaben
            .Where(a => (a.ErstelltVonId == meId || db.AufgabeZuweisungen.Any(z => z.AufgabeId == a.Id && z.AgentId == meId))
                && a.Faelligkeit != null && a.Faelligkeit >= vonUtc && a.Faelligkeit <= bisUtc)
            .OrderBy(a => a.Faelligkeit).Take(ProQuelleMax)
            .Select(a => new { a.Id, a.Titel, a.Faelligkeit, a.Status })
            .ToListAsync(ct))
        {
            eintraege.Add(new KalenderEintrag($"auf:{a.Id}", a.Titel, Lokal(a.Faelligkeit!.Value), null,
                false, KalenderQuelle.Aufgabe, $"/aufgaben/{a.Id}", AufgabeStatusAnzeige.IstAbgeschlossen(a.Status)));
        }

        // Meine offenen Wiedervorlagen (zuständig oder selbst erstellt).
        var wvs = await db.Wiedervorlagen
            .Where(w => !w.Erledigt && (w.ZustaendigerAgentId == meId || w.ErstelltVonId == meId)
                && w.FaelligAm >= vonUtc && w.FaelligAm <= bisUtc)
            .OrderBy(w => w.FaelligAm).Take(ProQuelleMax)
            .Select(w => new { w.Id, w.Notiz, w.FaelligAm, w.EntitaetTyp, w.EntitaetId })
            .ToListAsync(ct);
        if (wvs.Count > 0)
        {
            var refs = wvs.Select(w => (w.EntitaetTyp, w.EntitaetId)).Distinct().ToList();
            var map = await AktenReferenz.AufloesenAsync(db, refs, ct, darfVs, meId);
            foreach (var w in wvs)
            {
                map.TryGetValue((w.EntitaetTyp, w.EntitaetId), out var eltern);
                // Es ist MEINE Wiedervorlage → immer zeigen; aber den VS-Eltern-Namen für Nicht-Führung nicht leaken.
                var darfName = eltern.Anzeige is not null && !(eltern.Verschluss && !darfVs);
                var basis = darfName ? $"Wiedervorlage: {eltern.Anzeige}" : "Wiedervorlage fällig";
                var titel = string.IsNullOrWhiteSpace(w.Notiz) ? basis : $"{basis} · {w.Notiz}";
                eintraege.Add(new KalenderEintrag($"wv:{w.Id}", titel, Lokal(w.FaelligAm), null,
                    false, KalenderQuelle.Wiedervorlage, darfName ? eltern.Href : null));
            }
        }
    }

    // ---- „Behörden-Kalender": öffentliche Termine + Operationen + Observationen + Personen-Doks + Fraktions-Aktivitäten ----
    private async Task LadeBehoerdeAsync(AppDbContext db, DateTime vonUtc, DateTime bisUtc, bool darfVs,
        List<KalenderEintrag> eintraege, CancellationToken ct)
    {
        // Öffentliche Termine (die Aufsicht/Führung sieht zusätzlich alle Stufen).
        foreach (var t in await db.Termine.FuerBehoerde(darfVs)
            .Where(t => t.Beginn <= bisUtc && (t.Ende ?? t.Beginn) >= vonUtc)
            .OrderBy(t => t.Beginn).Take(ProQuelleMax)
            .Select(t => new { t.Id, t.Titel, t.Beginn, t.Ende, t.Ganztaegig, t.Status })
            .ToListAsync(ct))
        {
            eintraege.Add(new KalenderEintrag($"tm:{t.Id}", t.Titel, Lokal(t.Beginn), LokalOpt(t.Ende),
                t.Ganztaegig, KalenderQuelle.Termin, $"/kalender/{t.Id}", TerminStatusAnzeige.IstHinfaellig(t.Status)));
        }

        // Operationen (Verschlusssache-gefiltert).
        foreach (var o in await db.Operationen
            .Where(o => (darfVs || !o.IstVerschlusssache)
                && o.Beginn != null && o.Beginn <= bisUtc && (o.Ende ?? o.Beginn) >= vonUtc)
            .OrderBy(o => o.Beginn).Take(ProQuelleMax)
            .Select(o => new { o.Id, o.Titel, o.Beginn, o.Ende, o.Status })
            .ToListAsync(ct))
        {
            eintraege.Add(new KalenderEintrag($"op:{o.Id}", o.Titel, Lokal(o.Beginn!.Value), LokalOpt(o.Ende),
                false, KalenderQuelle.Operation, $"/operationen/{o.Id}", o.Status == OperationStatus.Abgebrochen));
        }

        // Überwachungsfenster (VS erbt von der Eltern-Person via INNER JOIN über die Pflicht-Nav).
        foreach (var ob in await db.Observationen
            .Where(ob => (darfVs || !ob.Person!.IstVerschlusssache)
                && ob.Beginn <= bisUtc && (ob.Ende ?? ob.Beginn) >= vonUtc)
            .OrderBy(ob => ob.Beginn).Take(ProQuelleMax)
            .Select(ob => new { ob.Id, ob.Ort, ob.Beginn, ob.Ende, ob.PersonId })
            .ToListAsync(ct))
        {
            var titel = string.IsNullOrWhiteSpace(ob.Ort) ? "Observation" : $"Observation – {ob.Ort}";
            eintraege.Add(new KalenderEintrag($"ob:{ob.Id}", titel, Lokal(ob.Beginn), LokalOpt(ob.Ende),
                false, KalenderQuelle.Observation, $"/personen/{ob.PersonId}"));
        }

        // Personen-Doks (alle – auch fremde; VS erbt von der Eltern-Person, gleiches sichere INNER-JOIN-Muster).
        foreach (var d in await db.PersonDoks
            .Where(d => (darfVs || !d.Person!.IstVerschlusssache)
                && d.Zeitpunkt >= vonUtc && d.Zeitpunkt <= bisUtc)
            .OrderBy(d => d.Zeitpunkt).Take(ProQuelleMax)
            .Select(d => new { d.Id, d.Zeitpunkt, d.Grund, d.PersonId, PersonName = d.Person!.Name })
            .ToListAsync(ct))
        {
            var titel = string.IsNullOrWhiteSpace(d.Grund) ? $"Dok: {d.PersonName}" : $"Dok: {d.PersonName} – {Kuerzen(d.Grund!)}";
            eintraege.Add(new KalenderEintrag($"dok:{d.Id}", titel, Lokal(d.Zeitpunkt), null,
                false, KalenderQuelle.PersonDok, $"/personen/{d.PersonId}?tab=doks"));
        }

        // Fraktions-Aktivitäten (VS erbt von der Eltern-Fraktion).
        foreach (var fa in await db.FraktionAktivitaeten
            .Where(fa => (darfVs || !fa.Fraktion!.IstVerschlusssache)
                && fa.Zeitpunkt >= vonUtc && fa.Zeitpunkt <= bisUtc)
            .OrderBy(fa => fa.Zeitpunkt).Take(ProQuelleMax)
            .Select(fa => new { fa.Id, fa.Titel, fa.Art, fa.Zeitpunkt, fa.FraktionId })
            .ToListAsync(ct))
        {
            var titel = string.IsNullOrWhiteSpace(fa.Art) ? fa.Titel : $"{fa.Titel} ({fa.Art})";
            eintraege.Add(new KalenderEintrag($"fa:{fa.Id}", titel, Lokal(fa.Zeitpunkt), null,
                false, KalenderQuelle.FraktionAktivitaet, $"/fraktionen/{fa.FraktionId}"));
        }
    }

    // UTC (in der DB als Unspecified/Utc abgelegt) → lokale Wandzeit ohne Kind (FullCalendar liest naiv-lokal).
    private static DateTime Lokal(DateTime utc)
        => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Unspecified);

    private static DateTime? LokalOpt(DateTime? utc) => utc is { } u ? Lokal(u) : null;

    private static string Kuerzen(string text, int max = 40)
        => text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
}
