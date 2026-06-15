using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Ein Personen-Dok: Protokoll eines Verhörs / einer Maßnahme. Voll auditiert und papierkorbfähig.
/// Der <see cref="Ausgang"/> kann den Lebensstatus der Person beeinflussen (siehe <c>PersonDokService</c>).
/// </summary>
[Table("PersonDoks")]
public class PersonDoc : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Zeitpunkt der Maßnahme (RP-Zeit, UTC gespeichert).</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    [Column("Grund")]
    public string? Reason { get; set; }

    /// <summary>Fraktionszugehörigkeit als Freitext – Rückfallebene, falls die Organisation (noch)
    /// nicht als Akte existiert. Existiert sie, wird stattdessen über <see cref="OrgTyp"/>/<see cref="OrgId"/> verknüpft.</summary>
    [Column("Fraktion")]
    public string? Faction { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c> oder <c>nameof(Personengruppe)</c>;
    /// null, wenn keine Akte verknüpft ist (dann zählt der Freitext <see cref="Fraktion"/>).</summary>
    [Column("OrgTyp")]
    public string? OrgType { get; set; }

    /// <summary>Id der verknüpften Fraktion bzw. Personengruppe (lose Verknüpfung ohne FK, analog der
    /// generischen Entitaet-Assoziationen). Der Name wird erst bei der Anzeige aufgelöst.</summary>
    public string? OrgId { get; set; }

    [Column("ErhalteneInformationen")]
    public string? ReceivedInformation { get; set; }

    [Column("Wahrheitsserum")]
    public bool TruthSerum { get; set; }

    [Column("Ausgang")]
    public MeasureOutcome Outcome { get; set; }

    /// <summary>Bei Amnestie-Spritze: Person lebt weiter, verliert aber ihre Erinnerung.</summary>
    [Column("GedaechtnisGeloescht")]
    public bool MemoryDeleted { get; set; }

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
