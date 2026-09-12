using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Handbook;

/// <summary>One term of the glossary, and the source of the explain-on-hover bubbles across the site.</summary>
/// <remarks>
/// <see cref="ShortDefinition"/> is plain text because it ends up inside a tooltip, where markup has nowhere to go.
/// <see cref="Synonyms"/> carries the other spellings the bubble should also trigger on, comma separated.
/// <para>
/// No soft delete: a term is hidden with <see cref="IsVisible"/> rather than deleted, so the shipped content cannot
/// resurrect it and the recycle bin does not fill with vocabulary.
/// </para>
/// </remarks>
[Table("HandbuchBegriffe")]
public class GlossaryTerm : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Begriff")]
    public string Term { get; set; } = string.Empty;

    /// <summary>Other spellings that mean the same thing, comma separated.</summary>
    [Column("Synonyme")]
    public string? Synonyms { get; set; }

    /// <summary>The bubble text: one sentence, plain.</summary>
    [Column("Kurzdefinition")]
    public string ShortDefinition { get; set; } = string.Empty;

    /// <summary>The longer entry on the glossary tab; null when the one sentence is the whole story.</summary>
    [Column("ErklaerungHtml")]
    public string? ExplanationHtml { get; set; }

    /// <summary>Article that goes into detail, for the "mehr dazu" link.</summary>
    [Column("ArtikelId")]
    public string? ArticleId { get; set; }
    public HandbookArticle? Article { get; set; }

    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    /// <summary>Stable handle of a shipped term; null on a hand-made one, which the seeder never touches.</summary>
    [Column("SeedSchluessel")]
    public string? SeedKey { get; set; }

    [Column("SeedRevision")]
    public int SeedRevision { get; set; }

    /// <summary>Set on the first editorial save; from then on the shipped content never overwrites this row.</summary>
    [Column("IstAngepasst")]
    public bool IsCustomised { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
