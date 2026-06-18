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

        return group;
    }

    private static IEnumerable<string> HazardRow(StatisticsTopEntry e)
        => new[] { e.Name, e.CaseNumber, e.Score.ToString(CultureInfo.InvariantCulture), HazardLevelLogic.Name(e.Level) };
}
