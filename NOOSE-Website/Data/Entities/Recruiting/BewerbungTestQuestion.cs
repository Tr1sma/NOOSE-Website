using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>A single question within a recruiting test.</summary>
[Table("BewerbungTestFragen")]
public class BewerbungTestQuestion : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TestId { get; set; } = string.Empty;
    public BewerbungTest? Test { get; set; }

    [Column("Typ")]
    public TestQuestionType Type { get; set; }

    [Column("Frage")]
    public string Prompt { get; set; } = string.Empty;

    [Column("Sortierung")]
    public int Sorting { get; set; }

    [Column("Pflicht")]
    public bool Required { get; set; } = true;

    [Column("Punkte")]
    public int Points { get; set; } = 1;

    /// <summary>Correct answer for Yes/No questions; null = not graded.</summary>
    [Column("RichtigJaNein")]
    public bool? CorrectYesNo { get; set; }

    /// <summary>Free-text keywords (newline/;/, separated) that should appear in the answer.</summary>
    [Column("Schlagwoerter")]
    public string? Keywords { get; set; }

    /// <summary>Minimum keyword hits for a free-text answer to count as correct; null = all.</summary>
    [Column("MindestTreffer")]
    public int? MinKeywordHits { get; set; }

    /// <summary>Serve options in authoring order instead of shuffled; for factually ordered options.</summary>
    [Column("ReihenfolgeFesthalten")]
    public bool KeepOptionOrder { get; set; }

    /// <summary>Let the applicant tick several options; an explicit author choice, never derived from the key.</summary>
    /// <remarks>Deriving it from "more than one option is flagged correct" would leak the key's shape to the form.</remarks>
    [Column("MehrfachauswahlErlaubt")]
    public bool AllowMultiple { get; set; }

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
