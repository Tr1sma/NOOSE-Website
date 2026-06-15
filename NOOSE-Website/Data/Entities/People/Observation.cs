using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Eine Observation: Überwachungs-/Beobachtungseintrag an einer Person – bewusst getrennt von den
/// Verhör-/Maßnahmen-Doks (<see cref="PersonDok"/>). Hält ein Beobachtungs-Zeitfenster
/// (<see cref="Beginn"/>/<see cref="Ende"/>), den <see cref="Ort"/>, die <see cref="Beobachtung"/> und ein
/// <see cref="Ergebnis"/> sowie den beobachtenden Agent. Optional lose mit einer Fraktion/Personengruppe
/// verknüpft (wie bei <see cref="PersonDok"/>, ohne FK). Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>); kein eigenes Aktenzeichen, keine Lebensstatus-Logik.
/// </summary>
[Table("Observationen")]
public class Observation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Beginn des Beobachtungsfensters (RP-Zeit, UTC gespeichert). Pflichtfeld.</summary>
    [Column("Beginn")]
    public DateTime Start { get; set; }

    /// <summary>Ende des Beobachtungsfensters (optional, RP-Zeit, UTC gespeichert).</summary>
    [Column("Ende")]
    public DateTime? End { get; set; }

    /// <summary>Ort der Beobachtung (Freitext).</summary>
    [Column("Ort")]
    public string? Location { get; set; }

    /// <summary>Die eigentliche Beobachtung (Freitext).</summary>
    [Column("Beobachtung")]
    public string? Sighting { get; set; }

    /// <summary>Ergebnis/Folgerung der Observation (Freitext, getrennt vom reinen Beobachtungstext).</summary>
    [Column("Ergebnis")]
    public string? Result { get; set; }

    /// <summary>Beobachtender Agent. Default beim Anlegen = erfassender Nutzer, kann aber abweichen
    /// (z. B. erfasste Beobachtung eines Kollegen). Null, wenn kein Agent gewählt oder der Agent gelöscht
    /// wurde (FK mit SetNull). Der erfassende Nutzer steht ohnehin im Audit-Log (<see cref="ErstelltVonId"/>).</summary>
    [Column("BeobachtenderAgentId")]
    public string? ObservingAgentId { get; set; }
    public Agent? ObservingAgent { get; set; }

    /// <summary>Typ der verknüpften Organisation: <c>nameof(Fraktion)</c> oder <c>nameof(Personengruppe)</c>;
    /// null, wenn keine Akte verknüpft ist.</summary>
    [Column("OrgTyp")]
    public string? OrgType { get; set; }

    /// <summary>Id der verknüpften Fraktion bzw. Personengruppe (lose Verknüpfung ohne FK, analog
    /// der generischen Entitaet-Assoziationen). Der Name wird erst bei der Anzeige aufgelöst.</summary>
    public string? OrgId { get; set; }

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
