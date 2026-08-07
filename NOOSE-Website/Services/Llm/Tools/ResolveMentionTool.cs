using System.Text;
using System.Text.Json;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Resolves @-mention tokens an agent pasted into a question into readable record names.</summary>
public sealed class ResolveMentionTool(IMentionService mentions) : INooseiTool
{
    public string Name => "loese_erwaehnung_auf";

    public string Description =>
        "Löst @-Erwähnungen der Form @{Typ:Id} in einem Text zu Aktennamen auf. "
        + "Nutze das, wenn der Agent eine Erwähnung in seiner Frage mitgeschickt hat.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["text"],
          "properties": {
            "text": { "type": "string", "description": "Text mit @{Typ:Id}-Erwähnungen." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var text = NooseiLimits.Text(arguments, "text");
        if (text is null)
        {
            return new NooseiToolResult("Bitte einen Text angeben.", null, true);
        }
        if (MentionParser.Parse(text).Count == 0)
        {
            return NooseiToolResult.Empty("Erwähnungen");
        }

        var segments = await mentions.ResolveAsync(
            text, context.Scope.MayClassifiedRead, context.Scope.MeId, cancellationToken, context.Scope.PartnerAgency);

        var sb = new StringBuilder();
        foreach (var segment in segments)
        {
            sb.Append(segment.Text);
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString().Trim()));
    }
}
