using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine terminierte Wiedervorlage/Erinnerung an einer beliebigen Akte (Person/Fraktion/…). Die Zuordnung erfolgt
/// polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> – analog zu <see cref="Quelle"/>, daher ohne
/// FK-Navigation. Wird der Termin (<see cref="FaelligAm"/>) erreicht, erzeugt der Hintergrund-Dienst eine
/// Benachrichtigung an den Zuständigen und die Follower der Akte. Voll auditiert und papierkorbfähig.
/// </summary>
public class Wiedervorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Typ der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    public string EntitaetId { get; set; } = string.Empty;

    /// <summary>Fälligkeitstermin (UTC). Überfällig = in der Vergangenheit bei noch offener Wiedervorlage.</summary>
    public DateTime FaelligAm { get; set; }

    /// <summary>Worum geht es / Grund der Wiedervorlage (Freitext, optional).</summary>
    public string? Notiz { get; set; }

    /// <summary>Zuständiger Agent (Identity-Id). Default = Ersteller; per <c>OnDelete SetNull</c> entkoppelt.</summary>
    public string? ZustaendigerAgentId { get; set; }

    /// <summary>True, sobald die Wiedervorlage abgehakt wurde.</summary>
    public bool Erledigt { get; set; }
    public DateTime? ErledigtAm { get; set; }
    public string? ErledigtVonId { get; set; }

    /// <summary>
    /// Gesetzt, sobald die Fälligkeits-Benachrichtigung verschickt wurde – verhindert, dass der wiederkehrende
    /// Hintergrund-Check dieselbe Wiedervorlage mehrfach meldet (Dedupe).
    /// </summary>
    public DateTime? BenachrichtigtAm { get; set; }

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
