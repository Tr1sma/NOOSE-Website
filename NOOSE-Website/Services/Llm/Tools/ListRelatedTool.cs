using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Lists the records linked to one record, masked to the asking agent's scope.</summary>
public sealed class ListRelatedTool(IDbContextFactory<AppDbContext> dbFactory) : INooseiTool
{
    public string Name => "zeige_verbindungen";

    public string Description =>
        "Listet die mit einer Akte verknüpften Akten auf, mit Bezeichnung der Verknüpfung. "
        + "Gut, um von einer Person zu ihren Fraktionen, Vorgängen und Operationen zu kommen.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ", "id"],
          "properties": {
            "typ": { "type": "string", "enum": {{NooseiRecordTypes.EnumJson}} },
            "id": { "type": "string" },
            "max": { "type": "integer", "minimum": 1, "maximum": 40 }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var type = NooseiRecordTypes.Clr(NooseiLimits.Text(arguments, "typ"));
        var id = NooseiLimits.Text(arguments, "id");
        if (type is null || id is null)
        {
            return NooseiToolResult.NotFound();
        }
        var max = NooseiLimits.Count(arguments, "max", 25);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, type, id, context.Scope, cancellationToken))
        {
            return NooseiToolResult.NotFound();
        }

        var links = await db.Links.AsNoTracking()
            .Where(l => (l.SourceType == type && l.SourceId == id) || (l.TargetType == type && l.TargetId == id))
            .OrderByDescending(l => l.CreatedAt)
            .Take(max)
            .Select(l => new { l.SourceType, l.SourceId, l.TargetType, l.TargetId, l.Label })
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
        {
            return NooseiToolResult.Empty("Verknüpfungen");
        }

        var others = links
            .Select(l => l.SourceType == type && l.SourceId == id ? (l.TargetType, l.TargetId) : (l.SourceType, l.SourceId))
            .Distinct()
            .ToList();

        // taskforce membership is this viewer's, and the classified flag is post-filtered below:
        // the resolver reports it but does not withhold anything on its own
        var resolved = await RecordsReference.ResolveAsync(db, others, cancellationToken,
            mayAllTaskforces: context.Scope.MayAllTaskforces, meId: context.Scope.MeId);

        var sb = new StringBuilder();
        var refs = new List<LlmContextRef>();
        sb.Append("Verknüpfungen (").Append(links.Count).AppendLine("):");
        foreach (var link in links)
        {
            var other = link.SourceType == type && link.SourceId == id
                ? (Type: link.TargetType, Id: link.TargetId)
                : (Type: link.SourceType, Id: link.SourceId);
            var label = string.IsNullOrWhiteSpace(link.Label) ? "Verknüpfung" : MentionParser.Strip(link.Label).Trim();

            sb.Append("• ").Append(label).Append(": ").Append(NooseiRecordTypes.German(other.Type)).Append(" | ");
            if (!resolved.TryGetValue(other, out var resolution))
            {
                sb.AppendLine("(nicht auflösbar)");
                continue;
            }
            if (resolution.Classified && !context.Scope.MayClassifiedRead)
            {
                sb.AppendLine("(Verschlusssache)");
                continue;
            }
            sb.Append(resolution.Display).Append(" | id=").AppendLine(other.Id);
            refs.Add(new LlmContextRef(other.Type, other.Id, resolution.Display));
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }
}
