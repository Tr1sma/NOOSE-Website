using System.Globalization;
using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Agency-wide chronicle of recent record events, already visibility-filtered by the service.</summary>
public sealed class RecentChangesTool(IGlobalChronikService chronik) : INooseiTool
{
    public string Name => "letzte_aenderungen";

    public string Description =>
        "Listet die letzten Änderungen an Akten behördenweit auf, neueste zuerst. "
        + "Gut für Fragen wie „was ist diese Woche passiert?\".";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "tage": { "type": "integer", "minimum": 1, "maximum": 30, "description": "Wie weit zurück; Standard 7." },
            "typen": { "type": "array", "items": { "type": "string", "enum": {{NooseiRecordTypes.ChronicleEnumJson}} } },
            "max": { "type": "integer", "minimum": 1, "maximum": 40 }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var days = NooseiLimits.Count(arguments, "tage", 7, 30);
        var max = NooseiLimits.Count(arguments, "max", 20);
        var types = NooseiLimits.Strings(arguments, "typen")
            .Select(t => NooseiRecordTypes.Clr(t, NooseiUse.Chronicle))
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        var now = DateTime.UtcNow;
        var query = new ChronikQuery(
            FromUtc: now.AddDays(-days),
            ToUtc: now,
            Types: types.Count > 0 ? types : null,
            MinEvents: max);

        var result = await chronik.GetEventsAsync(query, context.Actor, cancellationToken);
        var rows = result.Events.Take(max).ToList();
        if (rows.Count == 0)
        {
            return NooseiToolResult.Empty("Änderungen");
        }

        var sb = new StringBuilder();
        var refs = new List<LlmContextRef>();
        sb.Append("Änderungen der letzten ").Append(days).Append(" Tage (").Append(rows.Count).AppendLine("):");
        foreach (var e in rows)
        {
            sb.Append("• ").Append(e.Timestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))
                .Append(" | ").Append(NooseiRecordTypes.German(e.EntityType))
                .Append(" | ").Append(MentionParser.Strip(e.Name).Trim())
                .Append(" | ").Append(MentionParser.Strip(e.Title).Trim());
            if (!string.IsNullOrWhiteSpace(e.ActorName))
            {
                sb.Append(" (").Append(e.ActorName).Append(')');
            }
            sb.Append(" | id=").AppendLine(e.EntityId);
            refs.Add(new LlmContextRef(e.EntityType, e.EntityId, e.Name));
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }
}
