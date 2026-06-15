using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Parties;

/// <summary>
/// Mitgliedschaft einer Person in einer Partei – dediziertes Join-Entity. <see cref="IAuditable"/>
/// hält den Beitritts-/Änderungszeitpunkt fest. FK auf <see cref="Person"/> ist <c>Restrict</c> (sonst
/// kollidierende Cascade-Pfade, da auch die Partei auf diese Tabelle cascadet); FK auf Partei ist Cascade.
/// <para><see cref="ISoftDelete"/>: ein Austritt löscht die Zeile nicht hart, sondern markiert sie als beendet
/// (<c>GeloeschtAm</c> = Enddatum, <c>ErstelltAm</c> = Beitrittsdatum) – so bleibt der Mitgliedschafts-Verlauf
/// erhalten. Aktive Mitglieder = nicht gelöscht (greift automatisch über den globalen Soft-Delete-Filter).</para>
/// </summary>
[Table("ParteiMitglieder")]
public class PartyMember : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("ParteiId")]
    public string PartyId { get; set; } = string.Empty;
    public Party? Party { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Rolle innerhalb der Partei (Freitext, optional – z. B. „Vorsitz").</summary>
    [Column("Rolle")]
    public string? Role { get; set; }

    /// <summary>Gehört zur Führung/Leitung der Partei.</summary>
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
