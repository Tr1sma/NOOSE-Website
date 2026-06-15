using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NOOSE_Website.Authorization;
using NOOSE_Website.Infrastructure.Export;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Statistics;

namespace NOOSE_Website.Components.Common;

/// <summary>
/// CSV-Export der Statistik-Seite (Phase 8 / Block D). Eigene Minimal-API-Endpunkte (kein MVC),
/// nach dem Muster der Datei-Download-Endpunkte. Für jeden eingeloggten Agenten zugänglich; die Daten
/// werden über <see cref="IStatistikService"/> aus Sicht des Aufrufers VS-gefiltert erzeugt
/// (Nicht-Führung sieht keine Verschlusssachen-Aggregate). Jeder Export wird im Zugriffslog vermerkt.
/// </summary>
public static class StatisticsExportEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseStatisticsExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/statistik/export").RequireAuthorization(Policies.ActiveAgent);

        // ---- Alle Verteilungen + Zeitreihe als long-form Tabelle (Datensatz; Kategorie; Anzahl) ----
        group.MapGet("/verteilungen.csv", async (
            [FromServices] IStatisticsService statistics,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var report = await statistics.GetReportAsync(http.User.IsLeadership(), http.User.GetAgentId(),
                cancellationToken: cancellationToken);

            var rows = new List<IEnumerable<string>>();
            void Add(string record, IEnumerable<DistributionSegment> segments)
            {
                foreach (var s in segments)
                {
                    rows.Add(new[] { record, s.Designation, s.Count.ToString(CultureInfo.InvariantCulture) });
                }
            }

            Add("Personen nach Einstufung", report.PeopleByClassification);
            Add("Personen nach Gefährdung", report.PeopleByHazard);
            Add("Personen nach Lebensstatus", report.PeopleByLifeStatus);
            Add("Fraktionen nach Gefährdung", report.FactionsByHazard);
            Add("Maßnahme-Ausgänge", report.MeasureOutcomes);
            Add("Vorgänge nach Status", report.CasesByStatus);
            foreach (var m in report.TimeSeries)
            {
                rows.Add(new[] { "Zeitverlauf – Maßnahmen", m.Label, m.Measures.ToString(CultureInfo.InvariantCulture) });
                rows.Add(new[] { "Zeitverlauf – Neuzugänge", m.Label, m.NewEntries.ToString(CultureInfo.InvariantCulture) });
            }

            var bytes = CsvHelper.Generate(new[] { "Datensatz", "Kategorie", "Anzahl" }, rows);
            await access.LogViewAsync("Statistik", "verteilungen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-verteilungen.csv");
        });

        // ---- Gefährlichste Personen (alle bewerteten, Score > 0, absteigend) ----
        group.MapGet("/personen.csv", async (
            [FromServices] IStatisticsService statistics,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // topN = MaxValue: für den Export die vollständige Rangliste statt nur der Top-N der Seite.
            var report = await statistics.GetReportAsync(http.User.IsLeadership(), http.User.GetAgentId(),
                topN: int.MaxValue, cancellationToken: cancellationToken);
            var bytes = CsvHelper.Generate(
                new[] { "Name", "Aktenzeichen", "BedrohungsScore", "Gefährdungsstufe" },
                report.TopPeople.Select(HazardRow));
            await access.LogViewAsync("Statistik", "personen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-personen-gefaehrdung.csv");
        });

        // ---- Gefährlichste Fraktionen (alle bewerteten, Score > 0, absteigend) ----
        group.MapGet("/fraktionen.csv", async (
            [FromServices] IStatisticsService statistics,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var report = await statistics.GetReportAsync(http.User.IsLeadership(), http.User.GetAgentId(),
                topN: int.MaxValue, cancellationToken: cancellationToken);
            var bytes = CsvHelper.Generate(
                new[] { "Name", "Aktenzeichen", "BedrohungsScore", "Gefährdungsstufe" },
                report.TopFactions.Select(HazardRow));
            await access.LogViewAsync("Statistik", "fraktionen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-fraktionen-gefaehrdung.csv");
        });

        return group;
    }

    // Eine Top-Listen-Zeile als CSV-Felder (Score als invariante Ganzzahl, Stufe als Klartext).
    private static IEnumerable<string> HazardRow(StatisticsTopEntry e)
        => new[] { e.Name, e.CaseNumber, e.Score.ToString(CultureInfo.InvariantCulture), HazardLevelLogic.Name(e.Level) };
}
