using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine terminierte Wiedervorlage/Erinnerung an einer beliebigen Akte (Person/Fraktion/…). Die Zuordnung erfolgt
/// polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> – analog zu <see cref="Quelle"/>, daher ohne
/// FK-Navigation. Wird der Termin (<see cref="FaelligAm"/>) erreicht, erzeugt der Hintergrund-Dienst eine
/// Benachrichtigung an den Zuständigen und die Follower der Akte. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Wiedervorlagen")]
public class Wiedervorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Typ der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    [Column("EntitaetId")]
    public string EntitaetId { get; set; } = string.Empty;

    /// <summary>Fälligkeitstermin (UTC). Überfällig = in der Vergangenheit bei noch offener Wiedervorlage.</summary>
    [Column("FaelligAm")]
    public DateTime FaelligAm { get; set; }

    /// <summary>Worum geht es / Grund der Wiedervorlage (Freitext, optional).</summary>
    [Column("Notiz")]
    public string? Notiz { get; set; }

    /// <summary>Zuständiger Agent (Identity-Id). Default = Ersteller; per <c>OnDelete SetNull</c> entkoppelt.</summary>
    [Column("ZustaendigerAgentId")]
    public string? ZustaendigerAgentId { get; set; }

    /// <summary>True, sobald die Wiedervorlage abgehakt wurde.</summary>
    [Column("Erledigt")]
    public bool Erledigt { get; set; }
    [Column("ErledigtAm")]
    public DateTime? ErledigtAm { get; set; }
    [Column("ErledigtVonId")]
    public string? ErledigtVonId { get; set; }

    /// <summary>
    /// Gesetzt, sobald die Fälligkeits-Benachrichtigung verschickt wurde – verhindert, dass der wiederkehrende
    /// Hintergrund-Check dieselbe Wiedervorlage mehrfach meldet (Dedupe).
    /// </summary>
    [Column("BenachrichtigtAm")]
    public DateTime? BenachrichtigtAm { get; set; }

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
