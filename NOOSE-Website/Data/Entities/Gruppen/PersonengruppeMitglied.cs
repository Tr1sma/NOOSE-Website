using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Gruppen;

/// <summary>
/// Mitgliedschaft einer Person in einer Personengruppe – dediziertes Join-Entity. <see cref="IAuditable"/>
/// hält den Beitritts-/Änderungszeitpunkt fest. FK auf <see cref="Person"/> ist <c>Restrict</c> (sonst
/// kollidierende Cascade-Pfade, da auch die Gruppe auf diese Tabelle cascadet); FK auf Gruppe ist Cascade.
/// </summary>
public class PersonengruppeMitglied : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string PersonengruppeId { get; set; } = string.Empty;
    public Personengruppe? Personengruppe { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Rolle innerhalb der Gruppe (Freitext, optional).</summary>
    public string? Rolle { get; set; }

    /// <summary>Gehört zur Führung/Leitung der Gruppe.</summary>
    public bool IstLeitung { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
