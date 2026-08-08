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

    /// <summary>Structured brief payload (JSON) matching <c>NooseiSchemas.KurzbriefVersion</c>.</summary>
    [Column("KurzbriefJson")]
    public string? BriefJson { get; set; }

    /// <summary>Schema generation of <see cref="BriefJson"/>; lets the shape evolve without a migration.</summary>
    [Column("SchemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>Prompt generation at the time of writing; part of the staleness decision.</summary>
    [Column("PromptVersion")]
    public int PromptVersion { get; set; }

    /// <summary>Technical model id for forensics; never rendered to agents.</summary>
    [Column("Modell")]
    public string? Model { get; set; }

    [Column("GeneriertAm")]
    public DateTime GeneratedAt { get; set; }
}
