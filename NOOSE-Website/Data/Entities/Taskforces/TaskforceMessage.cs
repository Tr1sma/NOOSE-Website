using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>
/// Eine Chat-Nachricht im Team-Chat einer Taskforce (Phase 5d). <see cref="Text"/> enthält den Rohtext inklusive
/// inline-@-Verlinkungs-Tokens (<c>@{Typ:Id}</c>), die erst beim Anzeigen aufgelöst werden. <see cref="AutorName"/>
/// ist der Codename des Autors zum Sende-Zeitpunkt (denormalisiert, wie bei <c>Kommentar</c>). Sichtbarkeit erbt
/// von der Eltern-Taskforce (kein eigenes Verschlusssache-Flag). Voll auditiert und papierkorbfähig
/// (Soft-Delete = Nachricht „zurückgezogen").
/// </summary>
[Table("TaskforceNachrichten")]
public class TaskforceMessage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TaskforceId { get; set; } = string.Empty;
    public Taskforce? Taskforce { get; set; }

    /// <summary>Rohtext der Nachricht inkl. <c>@{Typ:Id}</c>-Verlinkungs-Tokens.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Autors zum Sende-Zeitpunkt (denormalisiert).</summary>
    [Column("AutorName")]
    public string? AuthorName { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
