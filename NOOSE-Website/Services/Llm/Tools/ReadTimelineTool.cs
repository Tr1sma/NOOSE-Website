using System.Globalization;
using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Reads a record's chronological event timeline; empty for records the agent may not see.</summary>
public sealed class ReadTimelineTool(ITimelineService timeline) : INooseiTool
{
    public string Name => "lies_zeitstrahl";

    public string Description =>
        "Liefert die Ereignisse einer Akte in zeitlicher Reihenfolge, neueste zuerst. "
        + "Gut für Fragen nach Verlauf, Entwicklung oder dem letzten Stand.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ", "id"],
          "properties": {
            "typ": { "type": "string", "enum": {{NooseiRecordTypes.EnumJson}} },
            "id": { "type": "string" },
            "seit": { "type": "string", "description": "ISO-Datum (JJJJ-MM-TT); nur neuere Ereignisse." },
            "max": { "type": "integer", "minimum": 1, "maximum": 40 }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var type = NooseiRecordTypes.Clr(NooseiLimits.Text(arguments, "typ"), NooseiUse.Read);
        var id = NooseiLimits.Text(arguments, "id");
        if (type is null || id is null)
        {
            return NooseiToolResult.NotFound();
        }
        var max = NooseiLimits.Count(arguments, "max", 20);

        DateTime? since = null;
        if (NooseiLimits.Text(arguments, "seit") is { } raw
            && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            since = parsed.ToUniversalTime();
        }

        var entries = await timeline.GetTimelineAsync(type, id, context.Actor, cancellationToken);
        var rows = entries
            .Where(e => since is null || e.Timestamp >= since)
            .Take(max)
            .ToList();
        if (rows.Count == 0)
        {
            return NooseiToolResult.Empty("Ereignisse");
        }

        var sb = new StringBuilder();
        sb.Append("Zeitstrahl (").Append(rows.Count).AppendLine("):");
        foreach (var entry in rows)
        {
            sb.Append("• ").Append(entry.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))
                .Append(" | ").Append(MentionParser.Strip(entry.Title).Trim());
            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                sb.Append(" — ").Append(MentionParser.Strip(entry.Detail).Trim());
            }
            if (!string.IsNullOrWhiteSpace(entry.ActorName))
            {
                sb.Append(" (").Append(entry.ActorName).Append(')');
            }
            sb.AppendLine();
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()),
            [new LlmContextRef(type, id, null)]);
    }
}
