using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Llm;

/// <summary>A manual correction to one agent's AI-token week: positive tops up, negative deducts.</summary>
/// <remarks>Kept apart from the request log so that log stays strictly one row per API call — every anomaly
/// rule and every cost statistic would otherwise need to exclude corrections.</remarks>
[Table("KiKontingentkorrekturen")]
public class LlmQuotaAdjustment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    [Column("Jahr")]
    public int Year { get; set; }

    [Column("Woche")]
    public int Week { get; set; }

    /// <summary>Signed quota tokens; a top-up lowers the week's net consumption by this amount.</summary>
    [Column("Tokens")]
    public long Tokens { get; set; }

    [Column("Grund")]
    public string Reason { get; set; } = string.Empty;

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }

    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }

    [Column("ErstelltVonName")]
    public string? CreatedByName { get; set; }
}
