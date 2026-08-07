using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Full-text search over the record database, scoped to the asking agent.</summary>
public sealed class SearchRecordsTool(ISearchService search) : INooseiTool
{
    public string Name => "suche_akten";

    public string Description =>
        "Durchsucht die Aktendatenbank nach Namen, Aktenzeichen, Aliassen und Stichworten. "
        + "Liefert Treffer mit Typ, Titel, Aktenzeichen und Id — die Id brauchst du für lies_akte.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["suchtext"],
          "properties": {
            "suchtext": { "type": "string", "description": "Freitext: Name, Aktenzeichen, Alias oder Stichwort." },
            "typen": { "type": "array", "items": { "type": "string", "enum": {{NooseiRecordTypes.EnumJson}} },
                       "description": "Auf diese Aktentypen einschränken; leer lassen für alle." },
            "unscharf": { "type": "boolean", "description": "Tippfehler-Toleranz einschalten." },
            "max": { "type": "integer", "minimum": 1, "maximum": 25 }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var text = NooseiLimits.Text(arguments, "suchtext");
        if (text is null)
        {
            return new NooseiToolResult("Bitte einen Suchtext angeben.", null, true);
        }

        var criteria = new SearchCriteria
        {
            Text = text,
            Fuzzy = arguments.TryGetProperty("unscharf", out var fuzzy) && fuzzy.ValueKind == JsonValueKind.True,
            Categories = NooseiLimits.Strings(arguments, "typen")
                .Select(NooseiRecordTypes.Clr)
                .Where(c => c is not null)
                .Select(c => c!)
                .ToList(),
        };
        var max = NooseiLimits.Count(arguments, "max", 10, 25);

        var groups = await search.SearchAsync(criteria, context.Scope, cancellationToken);
        var hits = groups.SelectMany(g => g.Hit).Take(max).ToList();
        if (hits.Count == 0)
        {
            return NooseiToolResult.Empty("Treffer");
        }

        var sb = new StringBuilder();
        var refs = new List<LlmContextRef>(hits.Count);
        sb.Append("Treffer (").Append(hits.Count).AppendLine("):");
        foreach (var hit in hits)
        {
            var type = hit.TargetType ?? hit.Category;
            sb.Append("• ").Append(NooseiRecordTypes.German(type))
                .Append(" | ").Append(hit.Title)
                .Append(" | Aktenzeichen: ").Append(string.IsNullOrWhiteSpace(hit.CaseNumber) ? "—" : hit.CaseNumber)
                .Append(" | id=").Append(hit.TargetId);
            if (!string.IsNullOrWhiteSpace(hit.Snippet))
            {
                var snippet = hit.Snippet.Length > NooseiLimits.MaxSnippetChars
                    ? hit.Snippet[..NooseiLimits.MaxSnippetChars] + "…"
                    : hit.Snippet;
                sb.Append(" | ").Append(snippet.Replace('\n', ' '));
            }
            sb.AppendLine();
            refs.Add(new LlmContextRef(type, hit.TargetId, hit.Title));
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }
}
