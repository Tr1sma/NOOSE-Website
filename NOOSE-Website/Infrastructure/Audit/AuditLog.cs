using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Infrastructure.Audit;

/// <summary>
/// Unveraenderlicher Aenderungs-Protokolleintrag: Wer hat Wann an welcher Entitaet Was geaendert.
/// Wird automatisch vom <see cref="AuditSaveChangesInterceptor"/> geschrieben.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Zeitpunkt { get; set; }

    /// <summary>Agent-Id (Identity-Key) des Verursachers; null bei System-/Hintergrundaktionen.</summary>
    public string? AgentId { get; set; }

    /// <summary>Anzeigename des Verursachers zum Zeitpunkt der Aktion (denormalisiert).</summary>
    public string? AgentName { get; set; }

    /// <summary>CLR-Typname der betroffenen Entitaet (z. B. "Person").</summary>
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Primaerschluessel der betroffenen Entitaet als Text.</summary>
    public string EntitaetId { get; set; } = string.Empty;

    public AuditAktion Aktion { get; set; }

    /// <summary>JSON mit den geaenderten Feldern (alt → neu). Null bei reinen Erstellungen.</summary>
    public string? AenderungenJson { get; set; }
}
