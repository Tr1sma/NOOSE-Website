using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>
/// Mitgliedschaft einer Person in einer Fraktion – dediziertes Join-Entity (statt generischer Verknüpfung),
/// weil es abfragbare Metadaten trägt: Fraktions-Rang und Leitungs-Flag. <see cref="IAuditable"/> hält den
/// Beitritts-/Änderungszeitpunkt fest. Der FK auf <see cref="Person"/> ist <c>Restrict</c> (sonst kollidierende
/// Cascade-Pfade, da auch die Fraktion auf diese Tabelle cascadet); der FK auf Fraktion ist Cascade.
/// <para><see cref="ISoftDelete"/>: ein Austritt löscht die Zeile nicht hart, sondern markiert sie als beendet
/// (<c>GeloeschtAm</c> = Enddatum, <c>ErstelltAm</c> = Beitrittsdatum) – so bleibt der Mitgliedschafts-Verlauf
/// erhalten. Aktive Mitglieder = nicht gelöscht (greift automatisch über den globalen Soft-Delete-Filter).</para>
/// </summary>
[Table("FraktionMitglieder")]
public class FactionMember : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Rang innerhalb der Fraktion – denormalisierte Kopie aus der Ränge-Liste der Fraktion.</summary>
    [Column("Rang")]
    public string? Rank { get; set; }

    /// <summary>Gehört zur Leaderschaft der Fraktion.</summary>
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
