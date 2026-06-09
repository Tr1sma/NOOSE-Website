using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Abstractions;

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
public class FraktionMitglied : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    /// <summary>Rang innerhalb der Fraktion – denormalisierte Kopie aus der Ränge-Liste der Fraktion.</summary>
    public string? Rang { get; set; }

    /// <summary>Gehört zur Leaderschaft der Fraktion.</summary>
    public bool IstLeitung { get; set; }

    // ---- IAuditable (ErstelltAm = Beitrittsdatum) ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete (GeloeschtAm = Austritts-/Enddatum) ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
