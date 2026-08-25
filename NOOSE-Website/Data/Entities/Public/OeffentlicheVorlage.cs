using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>One reusable text for a citizen-facing message, with its tokens still unexpanded.</summary>
/// <remarks>
/// Plain text, not HTML: both target columns (HinweisNachricht.Text, TicketNachricht.Text) are plain text and are
/// rendered as text outside, so an editor would promise formatting that is lost on every apply.
/// The stored text keeps its tokens — they are the payload here, and expansion happens only when the template is
/// applied, exactly like the three older token systems.
/// </remarks>
[Table("OeffentlicheVorlagen")]
public class OeffentlicheVorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Art")]
    public PublicTemplateKind Kind { get; set; }

    /// <summary>Shown in the picker, never sent outside.</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Raw body with tokens; see PublicTemplateRenderer for the set.</summary>
    [Column("Text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Only active templates are offered and only an active one is sent automatically.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    [Column("Reihenfolge")]
    public int SortOrder { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
