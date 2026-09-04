using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>One question of the public FAQ: the sub-section a visitor expands to read the answer.</summary>
/// <remarks>
/// <see cref="Anchor"/> is minted once from the question and then left alone. It is what a search hit and a shared
/// link address, so recomputing it per read would move it under a rename or a reorder and kill a link somebody had
/// already posted — the same failure a retracted address causes, and the reason the row carries a handle of its own
/// rather than the row id, which is an internal fact and a guessable one once it stands in a URL.
/// <para>
/// Nothing an author writes can forge a competing anchor: <c>HtmlCleanup</c> allows no <c>id</c> attribute, so the
/// answer body cannot carry one.
/// </para>
/// </remarks>
[Table("OeffentlicheFaqEintraege")]
public class OeffentlicheFaqEintrag : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("RubrikId")]
    public string RubrikId { get; set; } = string.Empty;
    public OeffentlicheFaqRubrik? Rubrik { get; set; }

    [Column("Frage")]
    public string Question { get; set; } = string.Empty;

    /// <summary>The fragment this question is addressed by; minted once, never recomputed.</summary>
    [Column("Anker")]
    public string Anchor { get; set; } = string.Empty;

    [Column("AntwortHtml")]
    public string? AnswerHtml { get; set; }

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("Sichtbar")]
    public bool IsVisible { get; set; } = true;

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
