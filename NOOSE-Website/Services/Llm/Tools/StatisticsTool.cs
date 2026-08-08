using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Models.Statistics;
using NOOSE_Website.Services.Statistics;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Aggregate figures: how the records are distributed, how the threat picture stands, what moved.</summary>
/// <remarks>
/// The one thing that could leak here is the leadership flag. It is derived from the asking agent's scope and
/// never passed as <c>true</c> — an aggregate built over classified records would report their existence in a
/// number even though no tool would name them.
/// </remarks>
public sealed class StatisticsTool(
    IStatisticsService statistics,
    IThreatStatisticsService threatStatistics,
    IThreatTrendService trends) : INooseiTool
{
    public string Name => "hole_kennzahlen";

    public string Description =>
        "Liefert aggregierte Kennzahlen statt einzelner Akten: Verteilungen und Spitzenreiter (ueberblick), "
        + "die Bedrohungslage in Zahlen (bedrohung) oder die stärksten Score-Veränderungen der letzten Zeit "
        + "(bewegung). Nutze es für Fragen nach der Lage, nach Verteilungen und nach Entwicklungen.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["bereich"],
          "properties": {
            "bereich": { "type": "string", "enum": ["ueberblick","bedrohung","bewegung"],
                         "description": "ueberblick = Verteilungen und Spitzenreiter, bedrohung = Lage in Zahlen, bewegung = Score-Veränderungen." },
            "tage": { "type": "integer", "minimum": 1, "maximum": 365,
                      "description": "Nur bei bewegung: Fenster der Veränderung, Standard 30 Tage." },
            "max": { "type": "integer", "minimum": 1, "maximum": 20, "description": "Länge der Ranglisten." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        // derived, never asserted: this flag is the whole visibility contract of an aggregate
        var mayClassified = context.Scope.MayClassifiedRead;
        var top = NooseiLimits.Count(arguments, "max", 5, 20);

        return NooseiLimits.Text(arguments, "bereich") switch
        {
            "bedrohung" => await ThreatAsync(mayClassified, cancellationToken),
            "bewegung" => await MoversAsync(context, top, NooseiLimits.Count(arguments, "tage", 30, 365), cancellationToken),
            "ueberblick" => await OverviewAsync(context, mayClassified, top, cancellationToken),
            _ => new NooseiToolResult("Bitte einen gültigen Bereich angeben: ueberblick, bedrohung oder bewegung.", null, true),
        };
    }

    private async Task<NooseiToolResult> OverviewAsync(NooseiToolContext context, bool mayClassified, int top, CancellationToken cancellationToken)
    {
        var report = await statistics.GetReportAsync(mayClassified, context.Scope.MeId, top, cancellationToken);
        var m = report.Metrics;

        var sb = new StringBuilder();
        sb.AppendLine("Bestand:");
        sb.Append("• Personen: ").Append(m.People)
            .Append(" | Fraktionen und Personengruppen: ").Append(m.FactionsAndGroups)
            .Append(" | Operationen: ").Append(m.Operations)
            .Append(" | offene Vorgänge: ").Append(m.OpenCases).AppendLine();
        sb.Append("• Verschlusssachen: ").Append(m.Classified)
            .Append(" | überfällige Akten: ").Append(m.StaleRecords).AppendLine();

        Distribution(sb, "Personen nach Einstufung", report.PeopleByClassification);
        Distribution(sb, "Personen nach Gefährdung", report.PeopleByHazard);
        Distribution(sb, "Personen nach Lebensstatus", report.PeopleByLifeStatus);
        Distribution(sb, "Fraktionen nach Gefährdung", report.FactionsByHazard);
        Distribution(sb, "Maßnahmen nach Ausgang", report.MeasureOutcomes);
        Distribution(sb, "Vorgänge nach Status", report.CasesByStatus);

        Top(sb, "Personen mit dem höchsten Bedrohungs-Score", report.TopPeople);
        Top(sb, "Fraktionen mit dem höchsten Bedrohungs-Score", report.TopFactions);

        if (!mayClassified)
        {
            sb.AppendLine("Hinweis: Die Zahlen umfassen nur nicht eingestufte Akten.");
        }
        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()));
    }

    private async Task<NooseiToolResult> ThreatAsync(bool mayClassified, CancellationToken cancellationToken)
    {
        var scope = new StatisticsScope(mayClassified, StatisticsRange.Months12);
        var head = await threatStatistics.GetHeadlineAsync(scope, cancellationToken);

        var sb = new StringBuilder("Bedrohungslage:").AppendLine();
        sb.Append("• bewertete Akten: ").Append(head.ScoredRecords)
            .Append(" | erhöht: ").Append(head.Elevated)
            .Append(" | kritisch: ").Append(head.Critical).AppendLine();
        sb.Append("• Durchschnitts-Score: ").Append(head.AverageScore.ToString("0.#"))
            .Append(" | Durchschnitts-Konfidenz: ").Append(head.AverageConfidence.ToString("0.#")).AppendLine();
        if (!mayClassified)
        {
            sb.AppendLine("Hinweis: Die Zahlen umfassen nur nicht eingestufte Akten.");
        }
        return new NooseiToolResult(sb.ToString());
    }

    private async Task<NooseiToolResult> MoversAsync(NooseiToolContext context, int top, int days, CancellationToken cancellationToken)
    {
        // takes the principal and filters itself, so no flag is derived here
        var movers = await trends.GetTopMoversAsync(context.Actor, days, top, cancellationToken);
        if (movers.Count == 0)
        {
            return NooseiToolResult.Empty($"Score-Veränderungen in den letzten {days} Tagen");
        }

        var sb = new StringBuilder($"Stärkste Score-Veränderungen der letzten {days} Tage:").AppendLine();
        var refs = new List<LlmContextRef>(movers.Count);
        foreach (var mover in movers)
        {
            sb.Append("• ").Append(NooseiRecordTypes.German(mover.EntityType))
                .Append(" | ").Append(mover.Name)
                .Append(" | ").Append(mover.FromScore).Append(" → ").Append(mover.ToScore)
                .Append(" (").Append(mover.Delta > 0 ? "+" : string.Empty).Append(mover.Delta).Append(')')
                .Append(" | id=").Append(mover.EntityId).AppendLine();
            refs.Add(new LlmContextRef(mover.EntityType, mover.EntityId, mover.Name));
        }
        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }

    private static void Distribution(StringBuilder sb, string title, IReadOnlyList<DistributionSegment> segments)
    {
        if (segments.Count == 0)
        {
            return;
        }
        sb.Append(title).Append(": ")
            .AppendLine(string.Join(", ", segments.Select(s => $"{s.Designation} {s.Count}")));
    }

    private static void Top(StringBuilder sb, string title, IReadOnlyList<StatisticsTopEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }
        sb.Append(title).AppendLine(":");
        foreach (var entry in entries)
        {
            sb.Append("• ").Append(entry.Name)
                .Append(" | Aktenzeichen: ").Append(string.IsNullOrWhiteSpace(entry.CaseNumber) ? "—" : entry.CaseNumber)
                .Append(" | Score: ").Append(entry.Score).AppendLine();
        }
    }
}
