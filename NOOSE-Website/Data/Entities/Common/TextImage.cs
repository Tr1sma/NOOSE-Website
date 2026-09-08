using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Image pasted into a plain-text field, referenced from that text by a mention token.</summary>
/// <remarks>
/// The file lives outside wwwroot and the text keeps only <c>@{TextImage:Id}</c>, so a chat row, a comment and an
/// audit entry stay readable text instead of a megabyte of base64. EntityType/EntityId name the record whose
/// visibility governs the picture: the delivery endpoint asks that record, never the token holder.
/// </remarks>
[Table("Textbilder")]
public class TextImage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }

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
