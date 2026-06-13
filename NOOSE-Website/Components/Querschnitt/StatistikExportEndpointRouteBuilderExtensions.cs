using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Infrastructure.Export;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistik;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Statistik;

namespace NOOSE_Website.Components.Querschnitt;

/// <summary>
/// CSV-Export der Statistik-Seite (Phase 8 / Block D). Eigene Minimal-API-Endpunkte (kein MVC),
/// nach dem Muster der Datei-Download-Endpunkte. Für jeden eingeloggten Agenten zugänglich; die Daten
/// werden über <see cref="IStatistikService"/> aus Sicht des Aufrufers VS-gefiltert erzeugt
/// (Nicht-Führung sieht keine Verschlusssachen-Aggregate). Jeder Export wird im Zugriffslog vermerkt.
/// </summary>
public static class StatistikExportEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseStatistikExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/statistik/export").RequireAuthorization(Policies.AktiverAgent);

        // ---- Alle Verteilungen + Zeitreihe als long-form Tabelle (Datensatz; Kategorie; Anzahl) ----
        group.MapGet("/verteilungen.csv", async (
            [FromServices] IStatistikService statistik,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var report = await statistik.GetReportAsync(http.User.IstFuehrung(), http.User.GetAgentId(),
                cancellationToken: cancellationToken);

            var zeilen = new List<IEnumerable<string>>();
            void Hinzufuegen(string datensatz, IEnumerable<VerteilungSegment> segmente)
            {
                foreach (var s in segmente)
                {
                    zeilen.Add(new[] { datensatz, s.Bezeichnung, s.Anzahl.ToString(CultureInfo.InvariantCulture) });
                }
            }

            Hinzufuegen("Personen nach Einstufung", report.PersonenNachEinstufung);
            Hinzufuegen("Personen nach Gefährdung", report.PersonenNachGefaehrdung);
            Hinzufuegen("Personen nach Lebensstatus", report.PersonenNachLebensstatus);
            Hinzufuegen("Fraktionen nach Gefährdung", report.FraktionenNachGefaehrdung);
            Hinzufuegen("Maßnahme-Ausgänge", report.MassnahmeAusgaenge);
            Hinzufuegen("Vorgänge nach Status", report.VorgaengeNachStatus);
            foreach (var m in report.Zeitverlauf)
            {
                zeilen.Add(new[] { "Zeitverlauf – Maßnahmen", m.Label, m.Massnahmen.ToString(CultureInfo.InvariantCulture) });
                zeilen.Add(new[] { "Zeitverlauf – Neuzugänge", m.Label, m.Neuzugaenge.ToString(CultureInfo.InvariantCulture) });
            }

            var bytes = CsvHelfer.Erzeuge(new[] { "Datensatz", "Kategorie", "Anzahl" }, zeilen);
            await zugriff.LogAnsichtAsync("Statistik", "verteilungen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-verteilungen.csv");
        });

        // ---- Gefährlichste Personen (alle bewerteten, Score > 0, absteigend) ----
        group.MapGet("/personen.csv", async (
            [FromServices] IStatistikService statistik,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // topN = MaxValue: für den Export die vollständige Rangliste statt nur der Top-N der Seite.
            var report = await statistik.GetReportAsync(http.User.IstFuehrung(), http.User.GetAgentId(),
                topN: int.MaxValue, cancellationToken: cancellationToken);
            var bytes = CsvHelfer.Erzeuge(
                new[] { "Name", "Aktenzeichen", "BedrohungsScore", "Gefährdungsstufe" },
                report.TopPersonen.Select(GefaehrdungZeile));
            await zugriff.LogAnsichtAsync("Statistik", "personen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-personen-gefaehrdung.csv");
        });

        // ---- Gefährlichste Fraktionen (alle bewerteten, Score > 0, absteigend) ----
        group.MapGet("/fraktionen.csv", async (
            [FromServices] IStatistikService statistik,
            [FromServices] IZugriffsLogService zugriff,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var report = await statistik.GetReportAsync(http.User.IstFuehrung(), http.User.GetAgentId(),
                topN: int.MaxValue, cancellationToken: cancellationToken);
            var bytes = CsvHelfer.Erzeuge(
                new[] { "Name", "Aktenzeichen", "BedrohungsScore", "Gefährdungsstufe" },
                report.TopFraktionen.Select(GefaehrdungZeile));
            await zugriff.LogAnsichtAsync("Statistik", "fraktionen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-fraktionen-gefaehrdung.csv");
        });

        return group;
    }

    // Eine Top-Listen-Zeile als CSV-Felder (Score als invariante Ganzzahl, Stufe als Klartext).
    private static IEnumerable<string> GefaehrdungZeile(StatistikTopEintrag e)
        => new[] { e.Name, e.Aktenzeichen, e.Score.ToString(CultureInfo.InvariantCulture), GefaehrdungsStufeLogic.Name(e.Stufe) };
}
