using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Eine typisierte Beziehung zwischen zwei Personen (Familie/Feind/…). Anders als die generische
/// <c>Verknuepfung</c> konkret typisiert mit echten FKs auf <see cref="Person"/>. Bewusst <b>ohne</b>
/// Collection-Navigation auf <see cref="Person"/> (zwei FKs auf dieselbe Tabelle → sonst kollidierende
/// Inverse-Navigations); geladen wird über <c>PersonAId == id || PersonBId == id</c>. FK mit
/// <c>Restrict</c> (kein Cascade-Pfad-Konflikt); Soft-Delete + Audit wie bei Doks.
/// </summary>
public class PersonBeziehung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string PersonAId { get; set; } = string.Empty;
    public Person? PersonA { get; set; }

    public string PersonBId { get; set; } = string.Empty;
    public Person? PersonB { get; set; }

    public BeziehungsTyp Typ { get; set; }

    public string? Notiz { get; set; }

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
