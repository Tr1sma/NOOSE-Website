using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Benachrichtigungen;

/// <summary>
/// Eine In-App-Benachrichtigung (Glocke) für genau einen Empfänger-Agenten. Voll auditiert und papierkorbfähig.
/// Bewusst KEIN polymorphes Ziel-Typ/Id-Paar, sondern ein direkter <see cref="Href"/> – die Ziele sind heterogen
/// (Akten, aber auch Sonderseiten wie /profil oder /). <see cref="ErstelltVonId"/> (vom Audit-Interceptor gestempelt)
/// ist der Auslöser und vom <see cref="EmpfaengerId"/> verschieden.
/// </summary>
public class Benachrichtigung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent-Id des Empfängers (Identity-User-Id).</summary>
    public string EmpfaengerId { get; set; } = string.Empty;

    public NotificationTyp Typ { get; set; }

    /// <summary>Denormalisierter Anzeigetext (ohne sensible Akten-/Verschlusssachen-Namen).</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Ziel-Link beim Anklicken (z. B. „/personen/{id}" oder „/profil"); null = nicht klickbar.</summary>
    public string? Href { get; set; }

    /// <summary>Zeitpunkt des Lesens; null = ungelesen (zählt fürs Badge).</summary>
    public DateTime? GelesenAm { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
