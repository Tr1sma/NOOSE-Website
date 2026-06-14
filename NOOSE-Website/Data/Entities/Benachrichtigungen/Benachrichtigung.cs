using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Benachrichtigungen;

/// <summary>
/// Eine In-App-Benachrichtigung (Glocke) für genau einen Empfänger-Agenten. Voll auditiert und papierkorbfähig.
/// Bewusst KEIN polymorphes Ziel-Typ/Id-Paar, sondern ein direkter <see cref="Href"/> – die Ziele sind heterogen
/// (Akten, aber auch Sonderseiten wie /profil oder /). <see cref="ErstelltVonId"/> (vom Audit-Interceptor gestempelt)
/// ist der Auslöser und vom <see cref="EmpfaengerId"/> verschieden.
/// </summary>
[Table("Benachrichtigungen")]
public class Benachrichtigung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent-Id des Empfängers (Identity-User-Id).</summary>
    [Column("EmpfaengerId")]
    public string EmpfaengerId { get; set; } = string.Empty;

    [Column("Typ")]
    public NotificationTyp Typ { get; set; }

    /// <summary>Denormalisierter Anzeigetext (ohne sensible Akten-/Verschlusssachen-Namen).</summary>
    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Ziel-Link beim Anklicken (z. B. „/personen/{id}" oder „/profil"); null = nicht klickbar.</summary>
    public string? Href { get; set; }

    /// <summary>Zeitpunkt des Lesens; null = ungelesen (zählt fürs Badge).</summary>
    [Column("GelesenAm")]
    public DateTime? GelesenAm { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
