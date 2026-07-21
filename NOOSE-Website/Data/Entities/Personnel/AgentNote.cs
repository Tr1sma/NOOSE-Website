using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>A personnel-file entry (commendation, remark, specialization, …); visible to all, created/deleted by leadership only.</summary>
[Table("AgentVermerke")]
public class AgentNote : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    [Column("Art")]
    public AgentNoteKind Kind { get; set; }

    [Column("ArtFrei")]
    public string? ArtFreetext { get; set; }

    [Column("Datum")]
    public DateTime EntryDate { get; set; }

    public string Text { get; set; } = string.Empty;

    [Column("AutorName")]
    public string? AuthorName { get; set; }

    [Column("Ausfuehrende")]
    public string? Ausfuehrende { get; set; }

    /// <summary>Executing agent ids parsed from the JSON column (empty on none/malformed).</summary>
    [NotMapped]
    public IReadOnlyList<string> ExecutorIds
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Ausfuehrende))
            {
                return Array.Empty<string>();
            }
            try { return JsonSerializer.Deserialize<List<string>>(Ausfuehrende) ?? (IReadOnlyList<string>)Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }
    }

    /// <summary>The type label, preferring the free-text override when set.</summary>
    [NotMapped]
    public string ArtLabel => string.IsNullOrWhiteSpace(ArtFreetext) ? AgentNoteKindDisplay.Name(Kind) : ArtFreetext!;

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
