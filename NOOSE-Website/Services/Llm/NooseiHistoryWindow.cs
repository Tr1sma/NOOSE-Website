using System.Text.Json;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>One stored conversation row, as the replay window sees it.</summary>
/// <remarks>Deliberately not the entity: the window is arithmetic over roles and lengths and has to be testable
/// without a database, the same way <see cref="LlmQuotaMath" /> is.</remarks>
public sealed record NooseiHistoryRow(
    string Role,
    string? Content,
    string? ToolName = null,
    string? ToolCallId = null,
    string? ToolCallsJson = null,
    bool IsError = false);

/// <summary>Builds the wire history of a follow-up turn: whole turns only, inside a token budget, with the older
/// tool output compacted.</summary>
/// <remarks>A row count is the wrong unit on both ends. One turn with four tool calls fills six rows, so twenty
/// rows remember three questions; twenty tool rows of 6.000 characters are ~35k tokens re-sent every round.</remarks>
public static class NooseiHistoryWindow
{
    /// <summary>Role of an assistant row that only carried tool calls. Its own role because the chat renders
    /// <c>user|assistant</c> — under either of those it would show up as an empty bubble.</summary>
    public const string ToolCallRole = "assistant_tools";

    /// <summary>Tool output outside the newest turn is clipped to this. The tool results are the mass of a
    /// conversation, and an older one only has to keep saying roughly what was found.</summary>
    public const int CompactToolChars = 600;

    /// <summary>Marker of a tool result that had to be replayed as plain context instead of a tool role.</summary>
    public const string FlattenedToolPrefix = "[Frühere Werkzeug-Antwort";

    /// <summary>Per-message overhead of the wire format, so forty empty rows do not come out free.</summary>
    private const int MessageOverheadTokens = 8;

    /// <summary>Rough token count. It only has to be monotone in the length — an exact tokenizer would buy nothing
    /// a budget with headroom does not already have.</summary>
    public static int Estimate(string? text) => MessageOverheadTokens + (text?.Length ?? 0) / 4;

    /// <summary>A tool result the model must read as context because its <c>tool_calls</c> partner is gone.</summary>
    public static string Flatten(string? toolName, string? content)
        => $"{FlattenedToolPrefix} · {toolName}]\n{content}";

    /// <summary>The history to replay, oldest first.</summary>
    /// <param name="rows">Stored rows of the conversation in ascending order.</param>
    /// <param name="sameScope">False drops every tool result: its text was authorised under rights the owner
    /// may no longer have.</param>
    public static List<LlmMessage> Build(
        IReadOnlyList<NooseiHistoryRow> rows, bool sameScope, int tokenBudget, int maxTurns)
    {
        var usable = rows.Where(r => !r.IsError && (sameScope || !IsToolShaped(r))).ToList();
        // a valid history starts at a question; whatever precedes the first one is half a turn
        var first = usable.FindIndex(r => r.Role == "user");
        if (first < 0 || tokenBudget <= 0)
        {
            return [];
        }

        var turns = Split(usable, first);
        // the newest turn keeps its detail, the older ones only their gist
        for (var t = 0; t < turns.Count - 1; t++)
        {
            turns[t] = turns[t].Select(Compact).ToList();
        }

        var chosen = new List<List<NooseiHistoryRow>>();
        var spent = 0;
        for (var t = turns.Count - 1; t >= 0 && chosen.Count < Math.Max(1, maxTurns); t--)
        {
            var turn = turns[t];
            var cost = Cost(turn);
            if (spent + cost > tokenBudget)
            {
                if (chosen.Count > 0)
                {
                    break;
                }
                // the exchange is worth more than the evidence: keep question and answer, drop the tool traffic
                turn = turn.Where(r => !IsToolShaped(r)).ToList();
                cost = Cost(turn);
                if (cost > tokenBudget)
                {
                    break;
                }
            }
            chosen.Insert(0, turn);
            spent += cost;
        }
        return Flatten(chosen);
    }

    private static bool IsToolShaped(NooseiHistoryRow row) => row.Role is "tool" or ToolCallRole;

    private static int Cost(List<NooseiHistoryRow> turn) => turn.Sum(r => Estimate(r.Content));

    /// <summary>Cuts the rows into turns. Only a <c>user</c> row starts one, which is what keeps a tool result
    /// together with the <c>tool_calls</c> message that asked for it.</summary>
    private static List<List<NooseiHistoryRow>> Split(List<NooseiHistoryRow> rows, int first)
    {
        var turns = new List<List<NooseiHistoryRow>>();
        for (var i = first; i < rows.Count; i++)
        {
            if (rows[i].Role == "user" || turns.Count == 0)
            {
                turns.Add([]);
            }
            turns[^1].Add(rows[i]);
        }
        return turns;
    }

    private static NooseiHistoryRow Compact(NooseiHistoryRow row)
        => row.Role == "tool" && row.Content is { } text && text.Length > CompactToolChars
            ? row with { Content = text[..CompactToolChars] + " …(gekürzt)" }
            : row;

    /// <summary>Turns the selected rows into wire messages, one turn at a time.</summary>
    /// <remarks>Both halves of a tool exchange have to be present or neither may go out: an unpaired <c>tool</c>
    /// role and a <c>tool_calls</c> message without answers are each a guaranteed 400. Every row stored before tool
    /// calls were kept is unpaired, so this cannot be left to the data being tidy. What has no partner becomes plain
    /// context instead — the shape a scope change produces anyway.</remarks>
    private static List<LlmMessage> Flatten(List<List<NooseiHistoryRow>> turns)
    {
        var messages = new List<LlmMessage>();
        foreach (var turn in turns)
        {
            var answered = turn
                .Where(r => r.Role == "tool" && r.ToolCallId is { Length: > 0 })
                .Select(r => r.ToolCallId!)
                .ToHashSet(StringComparer.Ordinal);
            var open = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in turn)
            {
                switch (row.Role)
                {
                    case "user":
                        messages.Add(LlmMessage.User(row.Content ?? string.Empty));
                        break;
                    case ToolCallRole when Answered(row.ToolCallsJson, answered) is { Count: > 0 } calls:
                        foreach (var call in calls)
                        {
                            open.Add(call.Id);
                        }
                        messages.Add(LlmMessage.Assistant(row.Content, calls));
                        break;
                    case "tool" when row.ToolCallId is { Length: > 0 } id && open.Contains(id):
                        messages.Add(LlmMessage.Tool(id, row.ToolName ?? "werkzeug", row.Content ?? string.Empty));
                        break;
                    case "tool" when !string.IsNullOrWhiteSpace(row.Content):
                        messages.Add(LlmMessage.Assistant(Flatten(row.ToolName, row.Content)));
                        break;
                    case "assistant" when !string.IsNullOrWhiteSpace(row.Content):
                        messages.Add(LlmMessage.Assistant(row.Content));
                        break;
                }
            }
        }
        return messages;
    }

    /// <summary>The calls of a stored round that actually have a result in this turn.</summary>
    private static List<LlmToolCall>? Answered(string? json, HashSet<string> answered)
        => Calls(json)?.Where(c => answered.Contains(c.Id)).ToList();

    /// <summary>Serialises the tool calls of an assistant round for storage.</summary>
    public static string? Serialize(IReadOnlyList<LlmToolCall>? calls)
        => calls is { Count: > 0 } ? JsonSerializer.Serialize(calls) : null;

    private static List<LlmToolCall>? Calls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<List<LlmToolCall>>(json)?
                .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
