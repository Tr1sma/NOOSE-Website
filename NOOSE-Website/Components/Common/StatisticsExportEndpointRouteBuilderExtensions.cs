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

/// <summary>CSV export of the statistics page; data is classification-filtered per caller and each export is access-logged.</summary>
public static class StatisticsExportEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapNooseStatisticsExportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/statistik/export").RequireAuthorization(Policies.ActiveAgent, Policies.InternalAgent);

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

        group.MapGet("/personen.csv", async (
            [FromServices] IStatisticsService statistics,
            [FromServices] IAccessLogService access,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            // full ranking, not page top-N
            var report = await statistics.GetReportAsync(http.User.IsLeadership(), http.User.GetAgentId(),
                topN: int.MaxValue, cancellationToken: cancellationToken);
            var bytes = CsvHelper.Generate(
                new[] { "Name", "Aktenzeichen", "BedrohungsScore", "Gefährdungsstufe" },
                report.TopPeople.Select(HazardRow));
            await access.LogViewAsync("Statistik", "personen", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-personen-gefaehrdung.csv");
        });

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

        group.MapGet("/bestand.csv", async (
            [FromServices] IInventoryStatisticsService inventory,
            [FromServices] IAccessLogService access,
            HttpContext http,
            [FromQuery] string? range,
            CancellationToken cancellationToken) =>
        {
            // visibility is re-derived from the principal; only the window may come from the query
            var scope = ScopeFor(http, range);

            var rows = new List<IEnumerable<string>>();
            AddGrid(rows, "Personen nach Einstufung", await inventory.GetClassificationAsync(scope, cancellationToken));
            AddGrid(rows, "Gefährdung", await inventory.GetHazardComparisonAsync(scope, cancellationToken));
            AddGrid(rows, "Vorgänge nach Status", await inventory.GetCaseFunnelAsync(scope, cancellationToken));
            AddGrid(rows, "Neue Akten", await inventory.GetGrowthAsync(scope, cancellationToken));
            AddGrid(rows, "Maßnahme-Ausgänge", await inventory.GetMeasureOutcomeTrendAsync(scope, cancellationToken));

            foreach (var ratio in await inventory.GetRecencyAsync(scope, cancellationToken))
            {
                rows.Add(["Aktualität", ratio.Label, "Im Fenster",
                    ratio.Value.ToString(CultureInfo.InvariantCulture)]);
                rows.Add(["Aktualität", ratio.Label, "Gesamt",
                    ratio.Total.ToString(CultureInfo.InvariantCulture)]);
            }

            var bytes = CsvHelper.Generate(["Datensatz", "Kategorie", "Reihe", "Wert"], rows);
            await access.LogViewAsync("Statistik", "bestand", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-bestand.csv");
        });

        group.MapGet("/durchlauf.csv", async (
            [FromServices] IThroughputStatisticsService throughput,
            [FromServices] IAccessLogService access,
            HttpContext http,
            [FromQuery] string? range,
            CancellationToken cancellationToken) =>
        {
            var scope = ScopeFor(http, range);

            var rows = new List<IEnumerable<string>>();
            AddGrid(rows, "Erfassung", await throughput.GetCaptureVersusMeasuresAsync(scope, cancellationToken));
            AddGrid(rows, "Vorgangsfluss", await throughput.GetOpenedVersusClosedAsync(scope, cancellationToken));

            foreach (var bucket in await throughput.GetCaseCycleTimeAsync(scope, cancellationToken))
            {
                rows.Add(["Zeit bis Abschluss", bucket.Label, "Vorgänge",
                    bucket.Count.ToString(CultureInfo.InvariantCulture)]);
            }
            foreach (var ratio in await throughput.GetFollowupPunctualityAsync(scope, cancellationToken))
            {
                rows.Add(["Wiedervorlagen", ratio.Label, "Anzahl",
                    ratio.Value.ToString(CultureInfo.InvariantCulture)]);
                rows.Add(["Wiedervorlagen", ratio.Label, "Grundgesamtheit",
                    ratio.Total.ToString(CultureInfo.InvariantCulture)]);
            }

            var bytes = CsvHelper.Generate(["Datensatz", "Kategorie", "Reihe", "Wert"], rows);
            await access.LogViewAsync("Statistik", "durchlauf", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-durchlauf.csv");
        });

        group.MapGet("/bedrohung.csv", async (
            [FromServices] IThreatStatisticsService threat,
            [FromServices] IAccessLogService access,
            HttpContext http,
            [FromQuery] string? range,
            CancellationToken cancellationToken) =>
        {
            var scope = ScopeFor(http, range);

            var rows = new List<IEnumerable<string>>();
            AddGrid(rows, "Score-Verteilung", await threat.GetScoreHistogramAsync(scope, cancellationToken));

            var headline = await threat.GetHeadlineAsync(scope, cancellationToken);
            rows.Add(["Kennzahl", "Bewertete Akten", "Anzahl",
                headline.ScoredRecords.ToString(CultureInfo.InvariantCulture)]);
            rows.Add(["Kennzahl", "Erhöht (ab 50)", "Anzahl",
                headline.Elevated.ToString(CultureInfo.InvariantCulture)]);
            rows.Add(["Kennzahl", "Kritisch (ab 75)", "Anzahl",
                headline.Critical.ToString(CultureInfo.InvariantCulture)]);
            rows.Add(["Kennzahl", "Durchschnitts-Score", "Punkte",
                headline.AverageScore.ToString("0.#", CultureInfo.InvariantCulture)]);

            var bytes = CsvHelper.Generate(["Datensatz", "Kategorie", "Reihe", "Wert"], rows);
            await access.LogViewAsync("Statistik", "bedrohung", cancellationToken);
            return Results.File(bytes, "text/csv; charset=utf-8", "statistik-bedrohung.csv");
        });

        return group;
    }

    private static IEnumerable<string> HazardRow(StatisticsTopEntry e)
        => new[] { e.Name, e.CaseNumber, e.Score.ToString(CultureInfo.InvariantCulture), HazardLevelLogic.Name(e.Level) };

    /// <summary>Scope from the caller's own claims plus the requested window; never trusts a visibility query parameter.</summary>
    private static StatisticsScope ScopeFor(HttpContext http, string? range)
        => new(http.User.MayClassifiedRead(), StatisticsRangeDisplay.Parse(range));

    /// <summary>Flattens a labelled grid into one CSV row per label and series.</summary>
    private static void AddGrid(List<IEnumerable<string>> rows, string record, ChartGrid grid)
    {
        for (var i = 0; i < grid.Labels.Count; i++)
        {
            foreach (var series in grid.Series)
            {
                var value = i < series.Values.Count ? series.Values[i] : 0;
                rows.Add([record, grid.Labels[i], series.Name, value.ToString("0", CultureInfo.InvariantCulture)]);
            }
        }
    }
}
