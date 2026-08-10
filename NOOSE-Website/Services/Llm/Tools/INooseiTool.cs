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

/// <summary>What NOOSEI may do with a record type.</summary>
/// <remarks>Four axes, not one flag, because the tools genuinely differ in reach. With a single list of "known
/// types" the narrowest tool decided for all of them, so the offering stood at eight kinds while the search, the
/// chronicle and the law module already carried more.</remarks>
[Flags]
public enum NooseiUse
{
    /// <summary>Nameable in a result, nothing else.</summary>
    None = 0,

    /// <summary>Openable as a record — <c>lies_akte</c>, <c>hole_kurzbrief</c>, <c>lies_zeitstrahl</c> and both
    /// graph tools. Requires a branch in <see cref="DossierContextBuilder" />.</summary>
    Read = 1,

    /// <summary>Enumerable by attribute in <c>finde_akten</c>. Requires a scope-filtered list service.</summary>
    List = 2,

    /// <summary>A category <c>suche_akten</c> may narrow to; <see cref="ISearchService" /> emits a group for it.</summary>
    Search = 4,

    /// <summary>A type <c>letzte_aenderungen</c> may filter on; the chronicle collects it.</summary>
    Chronicle = 8,
}

/// <summary>One record type as the model names it, and what the tools may do with it.</summary>
public sealed record NooseiRecordType(string German, string Clr, string Plural, NooseiUse Use = NooseiUse.None);

/// <summary>Maps the German record type names the model sees to the CLR names the services expect, and decides
/// which tool accepts which type.</summary>
public static class NooseiRecordTypes
{
    /// <summary>Order is load-bearing: it decides the order inside every schema enum, and the tool block is the
    /// cached prompt prefix. Append, never reorder.</summary>
    private static readonly NooseiRecordType[] All =
    [
        // record kinds proper: everything a tool may open
        new("Person", "Person", "Personen", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),
        new("Fraktion", "Faction", "Fraktionen", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),
        new("Personengruppe", "PersonGroup", "Personengruppen", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),
        new("Partei", "Party", "Parteien", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),
        new("Operation", "Operation", "Operationen", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),
        new("Vorgang", "Case", "Vorgänge", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),
        // no plain list service: membership decides who sees a taskforce, release who sees a document
        new("Taskforce", "Taskforce", "Taskforces", NooseiUse.Read | NooseiUse.Search | NooseiUse.Chronicle),
        new("Dokument", "Document", "Dokumente", NooseiUse.Read | NooseiUse.Search | NooseiUse.Chronicle),
        new("Gesetz", "Law", "Gesetze", NooseiUse.Read | NooseiUse.List | NooseiUse.Search | NooseiUse.Chronicle),

        // nameable and filterable, but no dossier: reading one would have to go through its own visibility helper
        new("Aufgabe", "Job", "Aufgaben", NooseiUse.Search | NooseiUse.Chronicle),
        new("Aktivität", "AgentActivity", "Aktivitäten", NooseiUse.Search),
        new("Personen-Dok", "PersonDoc", "Doks", NooseiUse.Search),

        // searchable but not openable: the global search emits these categories, so the model may narrow to them.
        // It could not before, although the hits arrived anyway — narrowing simply answered zero.
        new("Asservat", "EvidenceItem", "Asservate", NooseiUse.Search),
        new("Asservat-Eintrag", "EvidenceEntry", "Asservat-Einträge", NooseiUse.Search),
        new("Kassenbuchung", "KassenBuchung", "Kassenbuchungen", NooseiUse.Search),
        new("Finanzierungsantrag", "FinancingRequest", "Finanzierungsanträge", NooseiUse.Search),
        new("Entführung", "AgentAbduction", "Entführungen", NooseiUse.Search),
        new("Quelle", "Source", "Quellen", NooseiUse.Search),
        new("Kommentar", "Comment", "Kommentare", NooseiUse.Search),

        new("Termin", "Appointment", "Termine", NooseiUse.Search),
        new("Besprechung", "Meeting", "Besprechungen", NooseiUse.Search),
        new("Observation", "Observation", "Observationen", NooseiUse.Search),

        // label only. The chronicle and the graph emit these; without a German name the model reads an English
        // CLR name, takes it for a record kind and spends a round asking lies_akte for a rejected id
        new("Bewerbung", "Bewerbung", "Bewerbungen"),
    ];

    private static readonly Dictionary<string, NooseiRecordType> ByGerman =
        All.ToDictionary(t => t.German, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, NooseiRecordType> ByClr =
        All.ToDictionary(t => t.Clr, StringComparer.Ordinal);

    /// <summary>The enum values offered where a type must be openable as a record.</summary>
    public static readonly string EnumJson = Json(NooseiUse.Read);

    /// <summary>Types <c>finde_akten</c> can enumerate by attribute.</summary>
    public static readonly string ListableEnumJson = Json(NooseiUse.List);

    /// <summary>Categories <c>suche_akten</c> can narrow to.</summary>
    public static readonly string SearchableEnumJson = Json(NooseiUse.Search);

    /// <summary>Types <c>letzte_aenderungen</c> can filter on.</summary>
    public static readonly string ChronicleEnumJson = Json(NooseiUse.Chronicle);

    /// <summary>CLR name for a German type name; null when the name is unknown.</summary>
    public static string? Clr(string? german) => Find(german)?.Clr;

    /// <summary>CLR name only when the type also carries <paramref name="required" />.</summary>
    /// <remarks>Every tool that takes a type argument goes through this overload. A schema enum is a hint, not a
    /// guarantee — and a type that slips past it reaches a service that never gated it:
    /// <see cref="Visibility.IsRecordVisibleAsync" /> answers "visible" for every type it does not know.</remarks>
    public static string? Clr(string? german, NooseiUse required)
        => Find(german) is { } type && type.Use.HasFlag(required) ? type.Clr : null;

    /// <summary>German name of a type; never the CLR name, so no English label ever reaches the model.</summary>
    public static string German(string clr)
        => ByClr.TryGetValue(clr, out var type) ? type.German : "Eintrag";

    /// <summary>German plural of a record type, for count sentences.</summary>
    public static string Plural(string clr)
        => ByClr.TryGetValue(clr, out var type) ? type.Plural : German(clr);

    /// <summary>True when <c>lies_akte</c> accepts this type. A hit of any other type must not carry an id.</summary>
    public static bool IsReadable(string clr) => Can(clr, NooseiUse.Read);

    public static bool Can(string? clr, NooseiUse use)
        => clr is not null && ByClr.TryGetValue(clr, out var type) && type.Use.HasFlag(use);

    /// <summary>German names carrying a capability, for schema enums and for saying what a tool does accept.</summary>
    public static IReadOnlyList<string> Names(NooseiUse use)
        => All.Where(t => t.Use.HasFlag(use)).Select(t => t.German).ToList();

    private static NooseiRecordType? Find(string? german)
        => german is not null && ByGerman.TryGetValue(german, out var type) ? type : null;

    private static string Json(NooseiUse use)
        => "[" + string.Join(",", Names(use).Select(n => "\"" + n + "\"")) + "]";
}
