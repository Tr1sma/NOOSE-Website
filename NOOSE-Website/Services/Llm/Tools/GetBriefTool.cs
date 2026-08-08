using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Reads an already generated NOOSEI brief. Never generates one — a chat turn must not silently spend credits.</summary>
public sealed class GetBriefTool(IDossierSummaryService dossier) : INooseiTool
{
    public string Name => "hole_kurzbrief";

    public string Description =>
        "Liefert den bereits erstellten NOOSEI-Kurzbrief einer Akte, falls vorhanden. "
        + "Schneller Überblick, bevor du die ganze Akte liest.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ", "id"],
          "properties": {
            "typ": { "type": "string", "enum": {{NooseiRecordTypes.EnumJson}} },
            "id": { "type": "string" }
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

        var view = await dossier.GetAsync(type, id, context.Actor, cancellationToken);
        if (view is null)
        {
            return NooseiToolResult.NotFound();
        }
        if (view.Brief is not { } brief)
        {
            return new NooseiToolResult("Für diese Akte gibt es noch keinen Kurzbrief.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Kurzbrief:");
        sb.Append("TL;DR: ").AppendLine(brief.Tldr);
        foreach (var point in brief.Kernpunkte)
        {
            sb.Append("• ").AppendLine(point);
        }
        if (!string.IsNullOrWhiteSpace(brief.EinstufungBewertung))
        {
            sb.Append("Einstufung: ").AppendLine(brief.EinstufungBewertung);
        }
        sb.Append("Risiko: ").Append(brief.Risiko.Stufe);
        if (!string.IsNullOrWhiteSpace(brief.Risiko.Begruendung))
        {
            sb.Append(" — ").Append(brief.Risiko.Begruendung);
        }
        sb.AppendLine();
        if (view.IsStale)
        {
            sb.AppendLine("Hinweis: Die Akte wurde seit diesem Kurzbrief geändert.");
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), [new LlmContextRef(type, id, null)]);
    }
}
