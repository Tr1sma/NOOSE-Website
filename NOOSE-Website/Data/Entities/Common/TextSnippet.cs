using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>A phrase an agent keeps for themselves and drops into the fields they type in; owned by one agent, hard-deletable.</summary>
/// <remarks>
/// Not a sixth <c>Vorlagen</c> table: those are maintained by leadership for everyone, this one belongs to a single
/// account and nobody else ever reads it. The placeholders are the document token world (<c>{{Name}}</c>), expanded
/// when the snippet is inserted and never when it is saved - a stored snippet keeps its tokens, they are its payload.
/// </remarks>
[Table("Textbausteine")]
public class TextSnippet : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    [Column("Bezeichnung")]
    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    [Column("Reihenfolge")]
    public int Sorting { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
