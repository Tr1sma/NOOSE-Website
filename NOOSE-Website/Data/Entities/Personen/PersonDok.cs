using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Ein Personen-Dok: Protokoll eines Verhörs / einer Maßnahme. Voll auditiert und papierkorbfähig.
/// Der <see cref="Ausgang"/> kann den Lebensstatus der Person beeinflussen (siehe <c>PersonDokService</c>).
/// </summary>
public class PersonDok : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Zeitpunkt der Maßnahme (RP-Zeit, UTC gespeichert).</summary>
    public DateTime Zeitpunkt { get; set; }

    public string? Grund { get; set; }

    /// <summary>Fraktionszugehörigkeit als Freitext – Rückfallebene, falls die Organisation (noch)
    /// nicht als Akte existiert. Existiert sie, wird stattdessen über <see cref="OrgTyp"/>/<see cref="OrgId"/> verknüpft.</summary>
    public string? Fraktion { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c> oder <c>nameof(Personengruppe)</c>;
    /// null, wenn keine Akte verknüpft ist (dann zählt der Freitext <see cref="Fraktion"/>).</summary>
    public string? OrgTyp { get; set; }

    /// <summary>Id der verknüpften Fraktion bzw. Personengruppe (lose Verknüpfung ohne FK, analog der
    /// generischen Entitaet-Assoziationen). Der Name wird erst bei der Anzeige aufgelöst.</summary>
    public string? OrgId { get; set; }

    public string? ErhalteneInformationen { get; set; }

    public bool Wahrheitsserum { get; set; }

    public MassnahmeAusgang Ausgang { get; set; }

    /// <summary>Bei Amnestie-Spritze: Person lebt weiter, verliert aber ihre Erinnerung.</summary>
    public bool GedaechtnisGeloescht { get; set; }

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
