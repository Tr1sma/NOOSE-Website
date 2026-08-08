using System.Text;
using System.Text.Json;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Answers "which ones" and "how many" — the questions free-text search cannot.</summary>
/// <remarks>
/// Fans out over the per-type list services instead of querying EF directly. All six carry the identical
/// <c>GetListAsync(ViewerScope, ct)</c> signature, so the visibility filtering is inherited from the canonical
/// read path rather than rewritten here, where it could be got wrong. It is also what the list pages do.
/// </remarks>
public sealed class FilterRecordsTool(
    IPersonService people,
    IFactionService factions,
    IPersonGroupService groups,
    IPartyService parties,
    ICaseService cases,
    IOperationService operations) : INooseiTool
{
    public string Name => "finde_akten";

    public string Description =>
        "Findet Akten über Merkmale statt über Suchtext und zählt sie. Nutze es für Fragen der Form "
        + "welche alle und wie viele: alle Personen einer Einstufung, alle Fraktionen über einem "
        + "Bedrohungs-Score, alle seit X Tagen geänderten Akten. Für die Suche nach einem Namen nimm suche_akten.";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ"],
          "properties": {
            "typ": { "type": "string", "enum": {{NooseiRecordTypes.ListableEnumJson}},
                     "description": "Aktentyp, der durchgesehen wird." },
            "einstufung": { "type": "string", "enum": ["Unbekannt","Prüffall","Verdachtsfall","Gesichert staatsgefährdend"],
                            "description": "Nur Akten mit dieser Einstufung." },
            "lebensstatus": { "type": "string", "enum": ["Lebend","Tot","Flüchtig"],
                              "description": "Nur bei typ=Person; berücksichtigt das Respawn-Fenster." },
            "score_min": { "type": "integer", "minimum": 0, "maximum": 100,
                           "description": "Nur Akten mit Bedrohungs-Score ab diesem Wert (Person und Fraktion)." },
            "score_max": { "type": "integer", "minimum": 0, "maximum": 100 },
            "geaendert_seit_tagen": { "type": "integer", "minimum": 1, "maximum": 3650,
                                      "description": "Nur Akten, die in diesem Zeitraum zuletzt geändert wurden." },
            "nur_verschlusssache": { "type": "boolean", "description": "Nur als Verschlusssache geführte Akten." },
            "max": { "type": "integer", "minimum": 1, "maximum": 40, "description": "Höchstzahl gelisteter Akten." },
            "nur_anzahl": { "type": "boolean", "description": "Nur die Anzahl liefern, ohne Einzelakten." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var german = NooseiLimits.Text(arguments, "typ");
        var clr = NooseiRecordTypes.Clr(german);
        if (clr is null)
        {
            return new NooseiToolResult("Bitte einen gültigen Aktentyp angeben.", null, true);
        }

        var rows = await LoadAsync(clr, context.Scope, cancellationToken);
        if (rows is null)
        {
            return new NooseiToolResult($"Für {german} steht keine Merkmalssuche zur Verfügung.", null, true);
        }

        var filter = Filter.From(arguments);
        var matches = rows.Where(filter.Matches)
            .OrderByDescending(r => r.Score ?? -1)
            .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var headline = matches.Count == 1
            ? $"1 {NooseiRecordTypes.German(clr)} entspricht den Merkmalen{filter.Describe()}."
            : $"{matches.Count} {NooseiRecordTypes.Plural(clr)} entsprechen den Merkmalen{filter.Describe()}.";
        if (matches.Count == 0)
        {
            return new NooseiToolResult(headline);
        }

        var countOnly = arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("nur_anzahl", out var only) && only.ValueKind == JsonValueKind.True;
        if (countOnly)
        {
            return new NooseiToolResult(headline);
        }

        var max = NooseiLimits.Count(arguments, "max", 15);
        var shown = matches.Take(max).ToList();
        var sb = new StringBuilder(headline).AppendLine();
        foreach (var row in shown)
        {
            sb.Append("• ").Append(row.Title)
                .Append(" | Aktenzeichen: ").Append(string.IsNullOrWhiteSpace(row.CaseNumber) ? "—" : row.CaseNumber)
                .Append(" | Einstufung: ").Append(ClassificationDisplay.Name(row.Classification));
            if (row.Score is { } score)
            {
                sb.Append(" | Bedrohungs-Score: ").Append(score);
            }
            if (row.Extra is { Length: > 0 } extra)
            {
                sb.Append(" | ").Append(extra);
            }
            if (row.Secrecy != DocumentClassification.None)
            {
                sb.Append(" | Verschlusssache");
            }
            sb.Append(" | id=").Append(row.Id).AppendLine();
        }
        if (matches.Count > shown.Count)
        {
            sb.Append("(… ").Append(matches.Count - shown.Count).AppendLine(" weitere)");
        }

        var refs = shown.Select(r => new LlmContextRef(clr, r.Id, r.Title)).ToList();
        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }

    /// <summary>Loads one type through its own list service, which applies the viewer's scope.</summary>
    private async Task<List<Row>?> LoadAsync(string clr, ViewerScope scope, CancellationToken cancellationToken) => clr switch
    {
        nameof(Data.Entities.People.Person) => (await people.GetListAsync(scope, cancellationToken))
            .Select(p => new Row(p.Id, p.Name, p.CaseNumber, p.Classification, p.SecrecyLevel, p.ThreatScore,
                p.ModifiedAt, p.CreatedAt, "Lebensstatus: " + LifeStatusDisplay.Name(p.EffectiveLifeStatus),
                p.EffectiveLifeStatus))
            .ToList(),
        nameof(Data.Entities.Factions.Faction) => (await factions.GetListAsync(scope, cancellationToken))
            .Select(f => new Row(f.Id, f.Name, f.CaseNumber, f.Classification, f.SecrecyLevel, f.ThreatScore,
                f.ModifiedAt, f.CreatedAt))
            .ToList(),
        nameof(Data.Entities.Groups.PersonGroup) => (await groups.GetListAsync(scope, cancellationToken))
            .Select(g => new Row(g.Id, g.Name, g.CaseNumber, g.Classification, g.SecrecyLevel, null,
                g.ModifiedAt, g.CreatedAt))
            .ToList(),
        nameof(Data.Entities.Parties.Party) => (await parties.GetListAsync(scope, cancellationToken))
            .Select(p => new Row(p.Id, p.Name, p.CaseNumber, p.Classification, p.SecrecyLevel, null,
                p.ModifiedAt, p.CreatedAt))
            .ToList(),
        nameof(Data.Entities.Cases.Case) => (await cases.GetListAsync(scope, cancellationToken))
            .Select(c => new Row(c.Id, c.Title, c.CaseNumber, c.Classification, c.SecrecyLevel, null,
                c.ModifiedAt, c.CreatedAt, "Status: " + CaseStatusDisplay.Name(c.Status)))
            .ToList(),
        nameof(Data.Entities.Operations.Operation) => (await operations.GetListAsync(scope, cancellationToken))
            .Select(o => new Row(o.Id, o.Title, o.CaseNumber, o.Classification, o.SecrecyLevel, null,
                o.ModifiedAt, o.CreatedAt, "Status: " + OperationStatusDisplay.Name(o.Status)))
            .ToList(),
        _ => null,
    };

    /// <summary>One record reduced to what can be filtered on, whatever type it came from.</summary>
    private sealed record Row(
        string Id, string Title, string CaseNumber, Classification Classification, DocumentClassification Secrecy,
        int? Score, DateTime? ModifiedAt, DateTime CreatedAt, string? Extra = null, LifeStatus? LifeStatus = null)
    {
        /// <summary>When the record was last touched. The audit interceptor stamps <c>ModifiedAt</c> only on an
        /// update, so a record created yesterday and never edited still has none — falling through to
        /// <c>CreatedAt</c> is what keeps a "changed in the last N days" question from missing the newest files.</summary>
        public DateTime TouchedAt => ModifiedAt ?? CreatedAt;
    }

    private sealed record Filter(
        Classification? Classification, LifeStatus? LifeStatus, int? ScoreMin, int? ScoreMax,
        int? ChangedWithinDays, bool ClassifiedOnly)
    {
        public static Filter From(JsonElement args) => new(
            ParseLabel(NooseiLimits.Text(args, "einstufung"), ClassificationDisplay.All,
                ClassificationDisplay.Name, ClassificationDisplay.DefaultName),
            ParseLabel(NooseiLimits.Text(args, "lebensstatus"), LifeStatusDisplay.All,
                LifeStatusDisplay.Name, LifeStatusDisplay.DefaultName),
            Number(args, "score_min"),
            Number(args, "score_max"),
            Number(args, "geaendert_seit_tagen"),
            args.ValueKind == JsonValueKind.Object
                && args.TryGetProperty("nur_verschlusssache", out var vs) && vs.ValueKind == JsonValueKind.True);

        public bool Matches(Row row)
            => (Classification is not { } c || row.Classification == c)
                && (LifeStatus is not { } l || row.LifeStatus == l)
                && (ScoreMin is not { } min || row.Score >= min)
                && (ScoreMax is not { } max || row.Score <= max)
                && (ChangedWithinDays is not { } days || row.TouchedAt >= DateTime.UtcNow.AddDays(-days))
                && (!ClassifiedOnly || row.Secrecy != DocumentClassification.None);

        /// <summary>Repeats the applied filters, so a bare count cannot be read as a different question's answer.</summary>
        public string Describe()
        {
            var parts = new List<string>(6);
            if (Classification is { } c) { parts.Add("Einstufung " + ClassificationDisplay.Name(c)); }
            if (LifeStatus is { } l) { parts.Add("Lebensstatus " + LifeStatusDisplay.Name(l)); }
            if (ScoreMin is { } min) { parts.Add($"Score ab {min}"); }
            if (ScoreMax is { } max) { parts.Add($"Score bis {max}"); }
            if (ChangedWithinDays is { } days) { parts.Add($"in den letzten {days} Tagen angelegt oder geändert"); }
            if (ClassifiedOnly) { parts.Add("nur Verschlusssachen"); }
            return parts.Count == 0 ? " (ohne Einschränkung)" : " (" + string.Join(", ", parts) + ")";
        }

        private static int? Number(JsonElement args, string name)
            => args.ValueKind == JsonValueKind.Object
                && args.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number)
                    ? number
                    : null;

        /// <summary>Matches a German label back to its enum value. The live label is admin-overridable, so the
        /// code default and the raw member name are accepted too — otherwise a renamed label breaks the tool.</summary>
        private static T? ParseLabel<T>(string? text, IReadOnlyList<T> all, Func<T, string> label, Func<T, string> fallback)
            where T : struct, Enum
        {
            if (text is null) { return null; }
            foreach (var value in all)
            {
                if (text.Equals(label(value), StringComparison.OrdinalIgnoreCase)
                    || text.Equals(fallback(value), StringComparison.OrdinalIgnoreCase)
                    || text.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
            return null;
        }
    }
}
