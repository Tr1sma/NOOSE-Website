using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Eine Observation: Überwachungs-/Beobachtungseintrag an einer Person – bewusst getrennt von den
/// Verhör-/Maßnahmen-Doks (<see cref="PersonDok"/>). Hält ein Beobachtungs-Zeitfenster
/// (<see cref="Beginn"/>/<see cref="Ende"/>), den <see cref="Ort"/>, die <see cref="Beobachtung"/> und ein
/// <see cref="Ergebnis"/> sowie den beobachtenden Agent. Optional lose mit einer Fraktion/Personengruppe
/// verknüpft (wie bei <see cref="PersonDok"/>, ohne FK). Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>); kein eigenes Aktenzeichen, keine Lebensstatus-Logik.
/// </summary>
public class Observation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Beginn des Beobachtungsfensters (RP-Zeit, UTC gespeichert). Pflichtfeld.</summary>
    public DateTime Beginn { get; set; }

    /// <summary>Ende des Beobachtungsfensters (optional, RP-Zeit, UTC gespeichert).</summary>
    public DateTime? Ende { get; set; }

    /// <summary>Ort der Beobachtung (Freitext).</summary>
    public string? Ort { get; set; }

    /// <summary>Die eigentliche Beobachtung (Freitext).</summary>
    public string? Beobachtung { get; set; }

    /// <summary>Ergebnis/Folgerung der Observation (Freitext, getrennt vom reinen Beobachtungstext).</summary>
    public string? Ergebnis { get; set; }

    /// <summary>Beobachtender Agent. Default beim Anlegen = erfassender Nutzer, kann aber abweichen
    /// (z. B. erfasste Beobachtung eines Kollegen). Null, wenn kein Agent gewählt oder der Agent gelöscht
    /// wurde (FK mit SetNull). Der erfassende Nutzer steht ohnehin im Audit-Log (<see cref="ErstelltVonId"/>).</summary>
    public string? BeobachtenderAgentId { get; set; }
    public Agent? BeobachtenderAgent { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c> oder <c>nameof(Personengruppe)</c>;
    /// null, wenn keine Akte verknüpft ist.</summary>
    public string? OrgTyp { get; set; }

    /// <summary>Id der verknüpften Fraktion bzw. Personengruppe (lose Verknüpfung ohne FK, analog
    /// der generischen Entitaet-Assoziationen). Der Name wird erst bei der Anzeige aufgelöst.</summary>
    public string? OrgId { get; set; }

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
