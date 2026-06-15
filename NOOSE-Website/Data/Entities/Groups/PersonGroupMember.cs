using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Groups;

/// <summary>
/// Mitgliedschaft einer Person in einer Personengruppe – dediziertes Join-Entity. <see cref="IAuditable"/>
/// hält den Beitritts-/Änderungszeitpunkt fest. FK auf <see cref="Person"/> ist <c>Restrict</c> (sonst
/// kollidierende Cascade-Pfade, da auch die Gruppe auf diese Tabelle cascadet); FK auf Gruppe ist Cascade.
/// <para><see cref="ISoftDelete"/>: ein Austritt löscht die Zeile nicht hart, sondern markiert sie als beendet
/// (<c>GeloeschtAm</c> = Enddatum, <c>ErstelltAm</c> = Beitrittsdatum) – so bleibt der Mitgliedschafts-Verlauf
/// erhalten. Aktive Mitglieder = nicht gelöscht (greift automatisch über den globalen Soft-Delete-Filter).</para>
/// </summary>
[Table("PersonengruppeMitglieder")]
public class PersonGroupMember : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("PersonengruppeId")]
    public string PersonGroupId { get; set; } = string.Empty;
    public PersonGroup? PersonGroup { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Rolle innerhalb der Gruppe (Freitext, optional).</summary>
    [Column("Rolle")]
    public string? Role { get; set; }

    /// <summary>Gehört zur Führung/Leitung der Gruppe.</summary>
    [Column("IstLeitung")]
    public bool IsLead { get; set; }

    // ---- IAuditable (ErstelltAm = Beitrittsdatum) ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete (GeloeschtAm = Austritts-/Enddatum) ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
