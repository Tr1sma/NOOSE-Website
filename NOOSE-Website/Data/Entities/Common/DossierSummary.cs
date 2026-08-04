using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Cached AI dossier summary for a record, polymorphic via EntityType + EntityId.
/// Deliberately NOT IAuditable/ISoftDelete — a derived cache, replaced whenever the source content hash changes.</summary>
[Table("KiZusammenfassungen")]
public class DossierSummary
{
    public long Id { get; set; }

    /// <summary>CLR type name of the record (nameof(Person), nameof(Faction), …).</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>SHA-256 of the exact context text summarised; an unchanged hash reuses the summary without a new LLM call.</summary>
    [Column("InhaltHash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Short lead paragraph (sanitized HTML).</summary>
    [Column("KurzfassungHtml")]
    public string? TldrHtml { get; set; }

    /// <summary>Full summary body (sanitized HTML).</summary>
    [Column("ZusammenfassungHtml")]
    public string? SummaryHtml { get; set; }

    [Column("Modell")]
    public string? Model { get; set; }

    [Column("GeneriertAm")]
    public DateTime GeneratedAt { get; set; }
}
