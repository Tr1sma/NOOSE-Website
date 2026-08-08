using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Models.Threat;

namespace NOOSE_Website.Services.Llm.Tools;

/// <summary>Explains how a threat score came about, from the breakdown the score run already persisted.</summary>
/// <remarks>
/// The one thing here that is not just a number is <see cref="ThreatPartialScore.Driver" />: it names the records
/// that drove a partial score — members, conflicts, abductions — and it was written at score time, against nobody's
/// scope. So it is withheld unless the asker reads classified records; the numbers stay, and the note that says so
/// is unconditional, so its presence cannot be read as evidence about this particular record.
/// </remarks>
public sealed class ExplainThreatScoreTool(IDbContextFactory<AppDbContext> dbFactory) : INooseiTool
{
    public string Name => "erklaere_bedrohungsscore";

    public string Description =>
        "Erklärt den Bedrohungs-Score einer Person oder Fraktion: die Teilscores mit ihren Punkten und Obergrenzen, "
        + "das Mindestband aus der Einstufung, die Konfidenz und den Stand der Berechnung. "
        + "Nutze es für Fragen der Form „warum hat X diesen Score?\".";

    public JsonElement ParameterSchema { get; } = NooseiLimits.Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["typ", "id"],
          "properties": {
            "typ": { "type": "string", "enum": ["Person", "Fraktion"],
                     "description": "Nur diese beiden Aktenarten tragen einen Bedrohungs-Score." },
            "id": { "type": "string", "description": "Id der Akte, wie von suche_akten geliefert." }
          }
        }
        """);

    public async Task<NooseiToolResult> InvokeAsync(JsonElement arguments, NooseiToolContext context, CancellationToken cancellationToken = default)
    {
        var type = NooseiRecordTypes.Clr(NooseiLimits.Text(arguments, "typ"), NooseiUse.Read);
        var id = NooseiLimits.Text(arguments, "id");
        // a wrong type says nothing about any record, so it gets a plain answer instead of the NotFound wording
        if (type is not (nameof(Person) or nameof(Faction)))
        {
            return new NooseiToolResult(
                "Einen Bedrohungs-Score tragen nur Personen und Fraktionen.", null, true);
        }
        if (id is null)
        {
            return NooseiToolResult.NotFound();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, type, id, context.Scope, cancellationToken))
        {
            return NooseiToolResult.NotFound();
        }

        var scored = type == nameof(Person)
            ? await db.People.AsNoTracking().Where(p => p.Id == id)
                .Select(p => new Scored(p.Name, p.CaseNumber, p.ThreatScore, p.ThreatConfidence, p.ScoreCalculatedAt, p.ThreatDetailJson))
                .FirstOrDefaultAsync(cancellationToken)
            : await db.Factions.AsNoTracking().Where(f => f.Id == id)
                .Select(f => new Scored(f.Name, f.CaseNumber, f.ThreatScore, f.ThreatConfidence, f.ScoreCalculatedAt, f.ThreatDetailJson))
                .FirstOrDefaultAsync(cancellationToken);
        if (scored is null)
        {
            return NooseiToolResult.NotFound();
        }

        var title = $"{scored.Name} ({scored.CaseNumber})";
        var refs = new[] { new LlmContextRef(type, id, scored.Name) };
        var detail = Parse(scored.DetailJson);
        if (detail is null)
        {
            return new NooseiToolResult(
                $"Für {title} liegt keine Score-Berechnung vor. Der Score wird nachts neu bestimmt; "
                + "bis dahin gibt es dazu nichts zu erklären.", refs);
        }

        var sb = new StringBuilder();
        sb.Append("Bedrohungs-Score von ").Append(title).Append(": ");
        sb.AppendLine(scored.Score is { } score ? score.ToString() : "nicht bewertet");
        if (detail.Excluded is { Length: > 0 } excluded)
        {
            sb.Append("Von der Bewertung ausgenommen: ").AppendLine(Free(excluded));
        }
        if (scored.Confidence is { } confidence)
        {
            sb.Append("Konfidenz: ").Append(confidence).AppendLine(" %");
        }
        if (detail.ClassificationName is { Length: > 0 })
        {
            sb.Append("Einstufung: ").Append(detail.ClassificationName)
                .Append(" — Mindestband ").Append(detail.Base).AppendLine();
        }
        sb.Append("Inhaltliche Summe vor dem Band: ").Append(detail.Content.ToString("0.#")).AppendLine();
        if (detail.BandHint is { Length: > 0 } band)
        {
            sb.Append("Bandeinordnung: ").AppendLine(Free(band));
        }

        var mayDriver = context.Scope.MayClassifiedRead;
        if (detail.PartialScores.Count > 0)
        {
            sb.Append("— Teilscores (").Append(detail.PartialScores.Count).AppendLine(") —");
            foreach (var part in detail.PartialScores)
            {
                sb.Append("• ").Append(Free(part.Name)).Append(": ")
                    .Append(part.Points.ToString("0.#")).Append(" von ").Append(part.Cap.ToString("0.#"))
                    .Append(" Punkten (Rohwert ").Append(part.RawValue.ToString("0.#")).Append(')').AppendLine();
                if (mayDriver && part.Driver.Count > 0)
                {
                    sb.Append("  Treiber: ")
                        .AppendLine(string.Join("; ", part.Driver.Select(Free).Where(d => d.Length > 0)));
                }
            }
        }
        if (!mayDriver)
        {
            sb.AppendLine("Hinweis: Die Treiber hinter den Teilscores sind der Führung vorbehalten.");
        }

        if (detail.TriageFlag)
        {
            sb.Append("Zur Prüfung vorgemerkt");
            sb.AppendLine(detail.TriageHint is { Length: > 0 } hint ? ": " + Free(hint) : ".");
        }
        // the column, when the breakdown predates the field: an undated explanation invites reading it as current
        var when = detail.CalculatedAtUtc != default ? detail.CalculatedAtUtc : scored.CalculatedAt;
        if (when is { } stamp)
        {
            sb.Append("Berechnet am: ").AppendLine(stamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
        }

        return new NooseiToolResult(NooseiLimits.Clip(sb.ToString()), refs);
    }

    /// <summary>A breakdown written by an older score version stays readable or drops out; it never throws.</summary>
    private static ThreatScoreDetail? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<ThreatScoreDetail>(json, ThreatScoreService.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Mention tokens carry raw ids of possibly classified records, so they never travel.</summary>
    private static string Free(string? text) => MentionParser.Strip(text).Trim();

    private sealed record Scored(
        string Name, string CaseNumber, int? Score, int? Confidence, DateTime? CalculatedAt, string? DetailJson);
}
