using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Ein Personen-Dok: Protokoll eines Verhörs / einer Maßnahme. Voll auditiert und papierkorbfähig.
/// Der <see cref="Ausgang"/> kann den Lebensstatus der Person beeinflussen (siehe <c>PersonDokService</c>).
/// </summary>
[Table("PersonDoks")]
public class PersonDok : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Zeitpunkt der Maßnahme (RP-Zeit, UTC gespeichert).</summary>
    [Column("Zeitpunkt")]
    public DateTime Zeitpunkt { get; set; }

    [Column("Grund")]
    public string? Grund { get; set; }

    /// <summary>Fraktionszugehörigkeit als Freitext – Rückfallebene, falls die Organisation (noch)
    /// nicht als Akte existiert. Existiert sie, wird stattdessen über <see cref="OrgTyp"/>/<see cref="OrgId"/> verknüpft.</summary>
    [Column("Fraktion")]
    public string? Fraktion { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c> oder <c>nameof(Personengruppe)</c>;
    /// null, wenn keine Akte verknüpft ist (dann zählt der Freitext <see cref="Fraktion"/>).</summary>
    [Column("OrgTyp")]
    public string? OrgTyp { get; set; }

    /// <summary>Id der verknüpften Fraktion bzw. Personengruppe (lose Verknüpfung ohne FK, analog der
    /// generischen Entitaet-Assoziationen). Der Name wird erst bei der Anzeige aufgelöst.</summary>
    public string? OrgId { get; set; }

    [Column("ErhalteneInformationen")]
    public string? ErhalteneInformationen { get; set; }

    [Column("Wahrheitsserum")]
    public bool Wahrheitsserum { get; set; }

    [Column("Ausgang")]
    public MassnahmeAusgang Ausgang { get; set; }

    /// <summary>Bei Amnestie-Spritze: Person lebt weiter, verliert aber ihre Erinnerung.</summary>
    [Column("GedaechtnisGeloescht")]
    public bool GedaechtnisGeloescht { get; set; }

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
