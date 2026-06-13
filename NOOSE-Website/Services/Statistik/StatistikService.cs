using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistik;

namespace NOOSE_Website.Services.Statistik;

/// <inheritdoc cref="IStatistikService" />
public class StatistikService(IDbContextFactory<AppDbContext> dbFactory, IDashboardService dashboard) : IStatistikService
{
    /// <summary>Wie viele Monate die Zeitreihe zurückreicht (inkl. des laufenden Monats).</summary>
    private const int ZeitreiheMonate = 12;

    public async Task<StatistikReport> GetReportAsync(bool istFuehrung, string? meId, int topN = 10,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Ein Schnappschuss-Zeitpunkt für die gesamte Berechnung (wie DashboardService): hält den
        // effektiven Lebensstatus (Tot-/Respawn-Fenster) und die Monatsgrenzen über alle Teilschritte konsistent.
        var jetzt = DateTime.UtcNow;

        // KPI-Kacheln aus dem Dashboard wiederverwenden – damit die Zahlen 1:1 zum Lagezentrum passen.
        var kennzahlen = await dashboard.GetKennzahlenAsync(istFuehrung, meId, cancellationToken);

        // ---- 1) Personen nach Einstufung (flacher GroupBy-Count, alle Enum-Werte für stabile Legende) ----
        var personEinstufung = (await db.Personen
                .Where(p => istFuehrung || !p.IstVerschlusssache)
                .GroupBy(p => p.Einstufung)
                .Select(g => new { Wert = g.Key, Anzahl = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Wert, x => x.Anzahl);
        var personenNachEinstufung = EinstufungAnzeige.Alle
            .Select(e => new VerteilungSegment(EinstufungAnzeige.Name(e), personEinstufung.GetValueOrDefault(e)))
            .ToList();

        // ---- 2) Personen nach Gefährdung (Score flach laden, Bucketing in-memory wie im Dashboard) ----
        var personenNachGefaehrdung = await GefaehrdungVerteilungAsync(
            db.Personen.Where(p => istFuehrung || !p.IstVerschlusssache).Select(p => p.BedrohungsScore), cancellationToken);

        // ---- 3) Personen nach (effektivem) Lebensstatus ----
        // Das Tot-/Respawn-Fenster ist on-read (keine DB-Spalte): Status + TotBis flach laden, dann
        // mit LebensstatusLogic.Effektiv in-memory auf den tatsächlich geltenden Status abbilden.
        var lebensRoh = await db.Personen
            .Where(p => istFuehrung || !p.IstVerschlusssache)
            .Select(p => new { p.Lebensstatus, p.TotBis })
            .ToListAsync(cancellationToken);
        var lebensZaehlung = lebensRoh
            .GroupBy(x => LebensstatusLogic.Effektiv(x.Lebensstatus, x.TotBis, jetzt))
            .ToDictionary(g => g.Key, g => g.Count());
        var personenNachLebensstatus = LebensstatusAnzeige.Alle
            .Select(s => new VerteilungSegment(LebensstatusAnzeige.Name(s), lebensZaehlung.GetValueOrDefault(s)))
            .ToList();

        // ---- 4) Fraktionen nach Gefährdung ----
        var fraktionenNachGefaehrdung = await GefaehrdungVerteilungAsync(
            db.Fraktionen.Where(f => istFuehrung || !f.IstVerschlusssache).Select(f => f.BedrohungsScore), cancellationToken);

        // ---- 5) Maßnahme-Ausgänge der Personen-Doks (VS über die Eltern-Person, INNER JOIN) ----
        var ausgangZaehlung = (await db.PersonDoks
                .Where(d => istFuehrung || !d.Person!.IstVerschlusssache)
                .GroupBy(d => d.Ausgang)
                .Select(g => new { Wert = g.Key, Anzahl = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Wert, x => x.Anzahl);
        var massnahmeAusgaenge = MassnahmeAusgangAnzeige.Alle
            .Select(a => new VerteilungSegment(MassnahmeAusgangAnzeige.Name(a), ausgangZaehlung.GetValueOrDefault(a)))
            .ToList();

        // ---- 6) Vorgänge nach Status ----
        var statusZaehlung = (await db.Vorgaenge
                .Where(v => istFuehrung || !v.IstVerschlusssache)
                .GroupBy(v => v.Status)
                .Select(g => new { Wert = g.Key, Anzahl = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Wert, x => x.Anzahl);
        var vorgaengeNachStatus = VorgangStatusAnzeige.Alle
            .Select(s => new VerteilungSegment(VorgangStatusAnzeige.Name(s), statusZaehlung.GetValueOrDefault(s)))
            .ToList();

        // ---- 7) Top-Listen der gefährlichsten Akten (nur bewertete, Score > 0) ----
        var topPersonenRoh = await db.Personen
            .Where(p => (istFuehrung || !p.IstVerschlusssache) && p.BedrohungsScore != null && p.BedrohungsScore > 0)
            .OrderByDescending(p => p.BedrohungsScore)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Aktenzeichen, p.BedrohungsScore })
            .Take(topN)
            .ToListAsync(cancellationToken);
        var topPersonen = topPersonenRoh
            .Select(p => new StatistikTopEintrag(p.Name, p.Aktenzeichen, $"/personen/{p.Id}",
                p.BedrohungsScore ?? 0, GefaehrdungsStufeLogic.Aus(p.BedrohungsScore)))
            .ToList();

        var topFraktionenRoh = await db.Fraktionen
            .Where(f => (istFuehrung || !f.IstVerschlusssache) && f.BedrohungsScore != null && f.BedrohungsScore > 0)
            .OrderByDescending(f => f.BedrohungsScore)
            .ThenBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.Aktenzeichen, f.BedrohungsScore })
            .Take(topN)
            .ToListAsync(cancellationToken);
        var topFraktionen = topFraktionenRoh
            .Select(f => new StatistikTopEintrag(f.Name, f.Aktenzeichen, $"/fraktionen/{f.Id}",
                f.BedrohungsScore ?? 0, GefaehrdungsStufeLogic.Aus(f.BedrohungsScore)))
            .ToList();

        // ---- 8) 12-Monats-Zeitreihe (Maßnahmen + Neuzugänge) ----
        // Pomelo-sicher: nur die jeweilige Datumsspalte ab dem Stichtag flach laden, dann in-memory in
        // Monats-Slots bucketen (kein GroupBy über .Year/.Month → keine CASE-Übersetzung).
        var ersterDesMonats = new DateTime(jetzt.Year, jetzt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var stichtag = ersterDesMonats.AddMonths(-(ZeitreiheMonate - 1));

        var massnahmeZeitpunkte = await db.PersonDoks
            .Where(d => (istFuehrung || !d.Person!.IstVerschlusssache) && d.Zeitpunkt >= stichtag)
            .Select(d => d.Zeitpunkt)
            .ToListAsync(cancellationToken);
        var neuzugangZeitpunkte = await db.Personen
            .Where(p => (istFuehrung || !p.IstVerschlusssache) && p.ErstelltAm >= stichtag)
            .Select(p => p.ErstelltAm)
            .ToListAsync(cancellationToken);

        var deDe = CultureInfo.GetCultureInfo("de-DE");
        var zeitverlauf = new List<StatistikMonat>(ZeitreiheMonate);
        for (var i = 0; i < ZeitreiheMonate; i++)
        {
            var von = stichtag.AddMonths(i);
            var bis = von.AddMonths(1);
            var massnahmen = massnahmeZeitpunkte.Count(z => z >= von && z < bis);
            var neuzugaenge = neuzugangZeitpunkte.Count(z => z >= von && z < bis);
            zeitverlauf.Add(new StatistikMonat(von.Year, von.Month, von.ToString("MMM yy", deDe), massnahmen, neuzugaenge));
        }

        return new StatistikReport(kennzahlen, personenNachEinstufung, personenNachGefaehrdung, personenNachLebensstatus,
            fraktionenNachGefaehrdung, massnahmeAusgaenge, vorgaengeNachStatus, topPersonen, topFraktionen, zeitverlauf);
    }

    // Lädt die Score-Spalte flach und bucketet sie in-memory über GefaehrdungsStufeLogic (kleine Menge,
    // vermeidet eine CASE-Übersetzung – identisches Vorgehen wie im DashboardService).
    private static async Task<List<VerteilungSegment>> GefaehrdungVerteilungAsync(IQueryable<int?> scores,
        CancellationToken cancellationToken)
    {
        var werte = await scores.ToListAsync(cancellationToken);
        var zaehlung = werte
            .GroupBy(GefaehrdungsStufeLogic.Aus)
            .ToDictionary(g => g.Key, g => g.Count());
        return GefaehrdungsStufeLogic.Alle
            .Select(s => new VerteilungSegment(GefaehrdungsStufeLogic.Name(s), zaehlung.GetValueOrDefault(s)))
            .ToList();
    }
}
