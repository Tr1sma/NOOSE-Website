using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>The asking agent's own watchlist — the records they decided to keep an eye on.</summary>
/// <remarks>
/// Entries the service marks inaccessible are dropped without a word, not reported as blocked. A watchlist keeps
/// what an agent once had access to, so counting or naming the rest would turn their own list into an existence
/// oracle for records that have since been classified.
/// </remarks>
public sealed class MyRecordsTool(IWatchlistService watchlist) : INooseiTool
{
    public string Name => "meine_akten";

    public string Description =>
        "Listet die Akten auf der Beobachtungsliste des fragenden Agenten, neueste zuerst. "
        + "Nutze es für Fragen wie „was beobachte ich?\" oder „welche meiner Akten hat sich geändert?\".";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "max": { "type": "integer", "minimum": 1, "maximum": 40 }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var max = NooseiLimits.Count(arguments, "max", 25);
        var followed = await watchlist.GetFollowedResolvedAsync(context.Actor, cancellationToken);
        var rows = followed.Where(f => f.Accessible).Take(max).ToList();
        if (rows.Count == 0)
        {
            return NooseiToolResult.Empty("Akten auf der Beobachtungsliste");
        }

        var sb = new StringBuilder();
        sb.Append("Beobachtungsliste (").Append(rows.Count).AppendLine("):");
        var refs = new List<LlmContextRef>(rows.Count);
        foreach (var row in rows)
        {
            sb.Append("• ").Append(NooseiRecordTypes.German(row.Type))
                .Append(" | ").Append(row.Display)
                .Append(" | beobachtet seit ").Append(row.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy"));
            sb.AppendLine(NooseiRecordTypes.IsReadable(row.Type)
                ? " | id=" + row.Id
                : " | (nicht als Akte lesbar)");
            refs.Add(new LlmContextRef(row.Type, row.Id, row.Display));
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }
}
