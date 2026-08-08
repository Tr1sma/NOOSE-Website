using System.Security.Claims;
using System.Text.Json;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Actor and derived scope for one turn. Built fresh from the live principal, never rehydrated from storage —
/// a conversation resumed after a demotion is answered at the new scope.</summary>
public sealed record NooseiToolContext(ClaimsPrincipal Actor, ViewerScope Scope)
{
    public static NooseiToolContext From(ClaimsPrincipal actor) => new(actor, ViewerScope.From(actor));
}

/// <summary>German plain-text result of one tool call, already clipped, plus what it touched.</summary>
public sealed record NooseiToolResult(string Text, IReadOnlyList<LlmContextRef>? Refs = null, bool IsError = false)
{
    /// <summary>Deliberately identical for "does not exist" and "you may not see it" — anything else turns
    /// every tool into an existence oracle for classified records.</summary>
    public static NooseiToolResult NotFound()
        => new("Akte nicht gefunden oder für dich nicht sichtbar.", null, true);

    public static NooseiToolResult Empty(string what) => new($"Keine {what} gefunden.");
}

/// <summary>One read capability NOOSEI may call. Every implementation filters by the actor's own scope.</summary>
public interface INooseiTool
{
    string Name { get; }

    string Description { get; }

    JsonElement ParameterSchema { get; }

    Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default);
}

/// <summary>Caps every tool honours, so a single result cannot blow the context or the token budget.</summary>
public static class NooseiLimits
{
    public const int MaxToolResultChars = 6_000;
    public const int MaxCompactRecordChars = 2_500;
    public const int MaxRowsPerTool = 40;
    public const int MaxSnippetChars = 160;

    /// <summary>Trim a tool result to its budget.</summary>
    public static string Clip(string text, int max = MaxToolResultChars)
        => string.IsNullOrEmpty(text) ? string.Empty
            : text.Length <= max ? text
            : text[..max] + "\n[…] (gekürzt)";

    /// <summary>Reads a string argument; missing, wrong-typed or blank all yield null.</summary>
    public static string? Text(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!.Trim()
                : null;

    /// <summary>Reads a bounded integer argument.</summary>
    public static int Count(JsonElement args, string name, int fallback, int max = MaxRowsPerTool)
        => args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
                ? Math.Clamp(number, 1, max)
                : fallback;

    public static IReadOnlyList<string> Strings(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
            .Select(x => x.GetString()!.Trim())
            .ToList();
    }

    public static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement.Clone();
}

/// <summary>All tools NOOSEI may offer, resolved from DI.</summary>
public sealed class NooseiToolRegistry
{
    private readonly Dictionary<string, INooseiTool> _byName;

    public NooseiToolRegistry(IEnumerable<INooseiTool> tools)
    {
        _byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        Definitions = _byName.Values
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new LlmToolDefinition(t.Name, t.Description, t.ParameterSchema))
            .ToList();
    }

    /// <summary>Stable order: the tool block is the cached prompt prefix, and reordering it would cost cache hits.</summary>
    public IReadOnlyList<LlmToolDefinition> Definitions { get; }

    public INooseiTool? Find(string name) => _byName.GetValueOrDefault(name);
}

/// <summary>Maps the German record type names the model sees to the CLR names the services expect.</summary>
public static class NooseiRecordTypes
{
    private static readonly Dictionary<string, string> ToClr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Person"] = "Person",
        ["Fraktion"] = "Faction",
        ["Personengruppe"] = "PersonGroup",
        ["Partei"] = "Party",
        ["Operation"] = "Operation",
        ["Vorgang"] = "Case",
        ["Taskforce"] = "Taskforce",
        ["Dokument"] = "Document",
    };

    private static readonly Dictionary<string, string> ToGerman =
        ToClr.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);

    /// <summary>The enum values offered in every tool schema.</summary>
    public const string EnumJson = """["Person","Fraktion","Personengruppe","Partei","Operation","Vorgang","Taskforce","Dokument"]""";

    /// <summary>Types that can be listed wholesale by attribute. Taskforce and Dokument are missing on purpose:
    /// neither has a plain scope-filtered list service, and both are gated by membership or release instead.</summary>
    public const string ListableEnumJson = """["Person","Fraktion","Personengruppe","Partei","Operation","Vorgang"]""";

    private static readonly Dictionary<string, string> Plurals = new(StringComparer.Ordinal)
    {
        ["Person"] = "Personen",
        ["Faction"] = "Fraktionen",
        ["PersonGroup"] = "Personengruppen",
        ["Party"] = "Parteien",
        ["Operation"] = "Operationen",
        ["Case"] = "Vorgänge",
        ["Taskforce"] = "Taskforces",
        ["Document"] = "Dokumente",
    };

    public static string? Clr(string? german)
        => german is not null && ToClr.TryGetValue(german, out var clr) ? clr : null;

    public static string German(string clr) => ToGerman.GetValueOrDefault(clr, clr);

    /// <summary>German plural of a record type, for count sentences.</summary>
    public static string Plural(string clr) => Plurals.GetValueOrDefault(clr, German(clr));
}
