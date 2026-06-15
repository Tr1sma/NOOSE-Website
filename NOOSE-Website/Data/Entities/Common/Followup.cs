using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Eine terminierte Wiedervorlage/Erinnerung an einer beliebigen Akte (Person/Fraktion/…). Die Zuordnung erfolgt
/// polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> – analog zu <see cref="Quelle"/>, daher ohne
/// FK-Navigation. Wird der Termin (<see cref="FaelligAm"/>) erreicht, erzeugt der Hintergrund-Dienst eine
/// Benachrichtigung an den Zuständigen und die Follower der Akte. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Wiedervorlagen")]
public class Followup : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Typ der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Fälligkeitstermin (UTC). Überfällig = in der Vergangenheit bei noch offener Wiedervorlage.</summary>
    [Column("FaelligAm")]
    public DateTime DueAt { get; set; }

    /// <summary>Worum geht es / Grund der Wiedervorlage (Freitext, optional).</summary>
    [Column("Notiz")]
    public string? Note { get; set; }

    /// <summary>Zuständiger Agent (Identity-Id). Default = Ersteller; per <c>OnDelete SetNull</c> entkoppelt.</summary>
    [Column("ZustaendigerAgentId")]
    public string? ResponsibleAgentId { get; set; }

    /// <summary>True, sobald die Wiedervorlage abgehakt wurde.</summary>
    [Column("Erledigt")]
    public bool Done { get; set; }
    [Column("ErledigtAm")]
    public DateTime? DoneAt { get; set; }
    [Column("ErledigtVonId")]
    public string? DoneById { get; set; }

    /// <summary>
    /// Gesetzt, sobald die Fälligkeits-Benachrichtigung verschickt wurde – verhindert, dass der wiederkehrende
    /// Hintergrund-Check dieselbe Wiedervorlage mehrfach meldet (Dedupe).
    /// </summary>
    [Column("BenachrichtigtAm")]
    public DateTime? NotifiedAt { get; set; }

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
