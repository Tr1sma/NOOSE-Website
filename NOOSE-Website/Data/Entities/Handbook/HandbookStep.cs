using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Handbook;

/// <summary>One numbered card of a walkthrough, below the article text.</summary>
/// <remarks>
/// Structure rather than markup on purpose: the HTML filter strips anything it does not know, and a step rendered
/// as hand-written markup would lose its shape the first time an author saved the article. Steps belong to their
/// article and are replaced wholesale when the shipped content updates it.
/// </remarks>
[Table("HandbuchSchritte")]
public class HandbookStep : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("ArtikelId")]
    public string ArticleId { get; set; } = string.Empty;
    public HandbookArticle? Article { get; set; }

    [Column("Nummer")]
    public int Number { get; set; }

    [Column("IconName")]
    public string? IconName { get; set; }

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Plain text; the card is a card, not a document.</summary>
    [Column("Text")]
    public string Text { get; set; } = string.Empty;

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
