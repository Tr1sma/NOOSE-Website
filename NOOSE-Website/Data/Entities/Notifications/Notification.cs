using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Notifications;

/// <summary>
/// Eine In-App-Benachrichtigung (Glocke) für genau einen Empfänger-Agenten. Voll auditiert und papierkorbfähig.
/// Bewusst KEIN polymorphes Ziel-Typ/Id-Paar, sondern ein direkter <see cref="Href"/> – die Ziele sind heterogen
/// (Akten, aber auch Sonderseiten wie /profil oder /). <see cref="ErstelltVonId"/> (vom Audit-Interceptor gestempelt)
/// ist der Auslöser und vom <see cref="EmpfaengerId"/> verschieden.
/// </summary>
[Table("Benachrichtigungen")]
public class Notification : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent-Id des Empfängers (Identity-User-Id).</summary>
    [Column("EmpfaengerId")]
    public string RecipientId { get; set; } = string.Empty;

    [Column("Typ")]
    public NotificationType Type { get; set; }

    /// <summary>Denormalisierter Anzeigetext (ohne sensible Akten-/Verschlusssachen-Namen).</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Ziel-Link beim Anklicken (z. B. „/personen/{id}" oder „/profil"); null = nicht klickbar.</summary>
    public string? Href { get; set; }

    /// <summary>Zeitpunkt des Lesens; null = ungelesen (zählt fürs Badge).</summary>
    [Column("GelesenAm")]
    public DateTime? ReadAt { get; set; }

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
