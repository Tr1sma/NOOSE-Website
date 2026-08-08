using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOOSE_Website.Models.Llm;

/// <summary>The structured NOOSEI record brief. Shape is fixed by <c>NooseiSchemas.Kurzbrief</c> and enforced by the endpoint.</summary>
public sealed record DossierBrief(
    string Tldr,
    IReadOnlyList<string> Kernpunkte,
    string EinstufungBewertung,
    IReadOnlyList<BriefConnection> Verbindungen,
    IReadOnlyList<BriefEvent> Verlauf,
    IReadOnlyList<string> OffenePunkte,
    BriefRisk Risiko)
{
    /// <summary>Snake-case on the wire; the model sees German field names, C# keeps its own casing.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsEmpty => string.IsNullOrWhiteSpace(Tldr) && Kernpunkte.Count == 0 && Verbindungen.Count == 0;
}

public sealed record BriefConnection(string Wer, string? Art, string? Relevanz);

public sealed record BriefEvent(string? Wann, string Was);

public sealed record BriefRisk(string Stufe, string? Begruendung)
{
    /// <summary>Tolerant parse: an unexpected value falls back to Mittel rather than throwing away the whole brief.</summary>
    [JsonIgnore]
    public BriefRiskLevel Level => Stufe?.Trim().ToLowerInvariant() switch
    {
        "niedrig" or "low" => BriefRiskLevel.Niedrig,
        "hoch" or "high" => BriefRiskLevel.Hoch,
        _ => BriefRiskLevel.Mittel,
    };
}

public enum BriefRiskLevel
{
    Niedrig = 0,
    Mittel = 1,
    Hoch = 2,
}
