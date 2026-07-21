using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Meetings;

/// <summary>Agenda item; soft-deleted because links point at it without a real FK.</summary>
[Table("BesprechungPunkte")]
public class MeetingAgendaItem : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("BesprechungId")]
    public string MeetingId { get; set; } = string.Empty;
    public Meeting? Meeting { get; set; }

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Sparse ordering; new items get max + 10.</summary>
    [Column("Sortierung")]
    public int Sorting { get; set; }

    /// <summary>Sanitized notes HTML written during the meeting.</summary>
    [Column("NotizHtml")]
    public string? NotesHtml { get; set; }

    [Column("Erledigt")]
    public bool Done { get; set; }
    [Column("ErledigtAm")]
    public DateTime? DoneAt { get; set; }
    [Column("ErledigtVonId")]
    public string? DoneById { get; set; }

    /// <summary>Item this one was carried over from; no FK, the source lives in another meeting.</summary>
    [Column("UebernommenVonPunktId")]
    public string? CarriedFromItemId { get; set; }

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
