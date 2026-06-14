using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Eine typisierte Beziehung zwischen zwei Personen (Familie/Feind/…). Anders als die generische
/// <c>Verknuepfung</c> konkret typisiert mit echten FKs auf <see cref="Person"/>. Bewusst <b>ohne</b>
/// Collection-Navigation auf <see cref="Person"/> (zwei FKs auf dieselbe Tabelle → sonst kollidierende
/// Inverse-Navigations); geladen wird über <c>PersonAId == id || PersonBId == id</c>. FK mit
/// <c>Restrict</c> (kein Cascade-Pfad-Konflikt); Soft-Delete + Audit wie bei Doks.
/// </summary>
[Table("PersonBeziehungen")]
public class PersonBeziehung : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string PersonAId { get; set; } = string.Empty;
    public Person? PersonA { get; set; }

    public string PersonBId { get; set; } = string.Empty;
    public Person? PersonB { get; set; }

    [Column("Typ")]
    public BeziehungsTyp Typ { get; set; }

    [Column("Notiz")]
    public string? Notiz { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
