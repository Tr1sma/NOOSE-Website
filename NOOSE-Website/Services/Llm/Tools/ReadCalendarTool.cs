using System.Globalization;
using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Calendar;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>What is coming up: appointments, meetings, operations, observations, due jobs and followups, person
/// measures, faction activities and absences.</summary>
/// <remarks>
/// Takes <see cref="ICalendarService" /> and nothing else — deliberately no <c>IDbContextFactory</c>. Nine sources
/// each carry their own visibility rule (meeting agendas open on a clock, jobs on a restriction flag, absences on
/// the roster), and every one of them is already applied inside that service. Without a database handle there is
/// no path through this tool that could read around any of them.
/// </remarks>
public sealed class ReadCalendarTool(ICalendarService calendar) : INooseiTool
{
    private const int DefaultDays = 14;
    private const int MaxDays = 90;

    public string Name => "lies_kalender";

    public string Description =>
        "Liefert die anstehenden Termine, Besprechungen, Operationen, Observationen, fälligen Aufgaben und "
        + "Wiedervorlagen, Personen-Doks, Fraktions-Aktivitäten und Abmeldungen ab heute. Nutze es für Fragen "
        + "wie „was steht diese Woche an?\", „wann ist die nächste Besprechung?\" oder „wer ist nächste Woche "
        + "abgemeldet?\". Vergangenes findest du mit letzte_aenderungen.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "tage": { "type": "integer", "minimum": 1, "maximum": {{MaxDays}},
                      "description": "Fenster ab heute in Tagen; Standard {{DefaultDays}}." },
            "umfang": { "type": "string", "enum": ["behoerde", "meine"],
                        "description": "behoerde = alles Behördenweite (Standard), meine = nur die eigene Agenda des fragenden Agenten." },
            "max": { "type": "integer", "minimum": 1, "maximum": 40 }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var days = NooseiLimits.Count(arguments, "tage", DefaultDays, MaxDays);
        var max = NooseiLimits.Count(arguments, "max", 25);
        var mine = string.Equals(NooseiLimits.Text(arguments, "umfang"), "meine", StringComparison.OrdinalIgnoreCase);
        var mode = mine ? CalendarMode.My : CalendarMode.Authority;

        // from local midnight, not from now: "what is on this week" includes an appointment earlier today
        var fromUtc = DateTime.Now.Date.ToUniversalTime();
        var entries = await calendar.GetEntriesAsync(fromUtc, fromUtc.AddDays(days), context.Actor, mode, cancellationToken);

        var rows = entries
            .Where(e => !e.Obsolete)
            .OrderBy(e => e.StartLocal)
            .Take(max)
            .ToList();
        var scopeName = mine ? "eigene Agenda" : "Behörde";
        if (rows.Count == 0)
        {
            return new NooseiToolResult($"Für die nächsten {days} Tage ({scopeName}) steht nichts an.");
        }

        var sb = new StringBuilder();
        sb.Append("Kalender, nächste ").Append(days).Append(" Tage (").Append(scopeName).Append("), ")
            .Append(rows.Count).AppendLine(" Einträge:");
        var refs = new List<LlmContextRef>();
        // grouped by source, because the group heading is the only place a plain German label for the kind exists
        foreach (var group in rows.GroupBy(e => e.Source).OrderBy(g => (int)g.Key))
        {
            sb.Append("— ").Append(CalendarDisplay.Name(group.Key)).Append(" (").Append(group.Count()).AppendLine(") —");
            foreach (var entry in group)
            {
                sb.Append("• ").Append(When(entry)).Append(" | ").AppendLine(MentionParser.Strip(entry.Title).Trim());
                if (entry.EntityType is { Length: > 0 } type && entry.EntityId is { Length: > 0 } id)
                {
                    refs.Add(new LlmContextRef(type, id, entry.Title));
                }
            }
        }
        if (entries.Count > rows.Count)
        {
            sb.Append("(… ").Append(entries.Count - rows.Count).AppendLine(" weitere oder entfallene Einträge)");
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }

    private static string When(CalendarEntry entry)
    {
        var day = entry.StartLocal.ToString("ddd dd.MM.yyyy", CultureInfo.CurrentCulture);
        if (entry.WholeDay)
        {
            return entry.EndLocal is { } until && until.Date > entry.StartLocal.Date
                ? $"{day} bis {until:ddd dd.MM.yyyy} (ganztägig)"
                : $"{day} (ganztägig)";
        }
        var time = entry.StartLocal.ToString("HH:mm", CultureInfo.CurrentCulture);
        return entry.EndLocal is { } end && end > entry.StartLocal
            ? $"{day} {time}–{end:HH:mm}"
            : $"{day} {time}";
    }
}
