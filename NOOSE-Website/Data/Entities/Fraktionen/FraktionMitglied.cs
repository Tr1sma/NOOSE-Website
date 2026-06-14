using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Fraktionen;

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
public class FraktionMitglied : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Rang innerhalb der Fraktion – denormalisierte Kopie aus der Ränge-Liste der Fraktion.</summary>
    [Column("Rang")]
    public string? Rang { get; set; }

    /// <summary>Gehört zur Leaderschaft der Fraktion.</summary>
    [Column("IstLeitung")]
    public bool IstLeitung { get; set; }

    // ---- IAuditable (ErstelltAm = Beitrittsdatum) ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete (GeloeschtAm = Austritts-/Enddatum) ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
