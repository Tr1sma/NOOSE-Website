using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Llm;

/// <summary>One completed NOOSEI request. Deliberately not IAuditable/ISoftDelete — this table IS the record.</summary>
[Table("KiAnfragen")]
public class LlmRequestLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    [Column("Zeitpunkt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>ISO week the charge was booked against; stored, not derived — MySQL cannot convert UTC to local in SQL.</summary>
    [Column("BudgetJahr")]
    public int BudgetYear { get; set; }

    [Column("BudgetWoche")]
    public int BudgetWeek { get; set; }

    [Column("Funktion")]
    public LlmFeature Feature { get; set; }

    /// <summary>Technical model id, for forensics only; never rendered to agents.</summary>
    [Column("Modell")]
    public string? Model { get; set; }

    [Column("Anbieter")]
    public string? Provider { get; set; }

    [Column("TokensEingabe")]
    public int PromptTokens { get; set; }

    [Column("TokensAusgabe")]
    public int CompletionTokens { get; set; }

    [Column("TokensCache")]
    public int CachedTokens { get; set; }

    [Column("TokensDenken")]
    public int ReasoningTokens { get; set; }

    [Column("KostenUsd")]
    public decimal CostUsd { get; set; }

    /// <summary>What was charged to the weekly quota; 1.000 tokens are worth one cent of real cost.</summary>
    [Column("KontingentTokens")]
    public long QuotaTokens { get; set; }

    [Column("DauerMs")]
    public int DurationMs { get; set; }

    /// <summary>Tool rounds this turn spent before answering.</summary>
    [Column("Werkzeugrunden")]
    public int ToolRounds { get; set; }

    [Column("Erfolg")]
    public bool Success { get; set; }

    [Column("Fehler")]
    public string? ErrorMessage { get; set; }

    /// <summary>What the agent typed, in full.</summary>
    [Column("Eingabe")]
    public string? Prompt { get; set; }

    /// <summary>What NOOSEI answered, in full.</summary>
    [Column("Antwort")]
    public string? Answer { get; set; }

    /// <summary>Which records and tools were touched, as a compact JSON reference list. Never the injected text itself.</summary>
    [Column("Kontextrefs")]
    public string? ContextRefsJson { get; set; }

    /// <summary>SHA-256 of the normalised prompt; first pass of the near-identical-prompt rule.</summary>
    [Column("EingabeFingerabdruck")]
    public string? PromptFingerprint { get; set; }

    [Column("Auffaellig")]
    public bool IsAnomalous { get; set; }

    [Column("Auffaelligkeit")]
    public LlmAnomalyKind? AnomalyKind { get; set; }
}
