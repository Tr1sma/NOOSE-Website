using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Eine typisierte Beziehung zwischen zwei Personen (Familie/Feind/…). Anders als die generische
/// <c>Verknuepfung</c> konkret typisiert mit echten FKs auf <see cref="Person"/>. Bewusst <b>ohne</b>
/// Collection-Navigation auf <see cref="Person"/> (zwei FKs auf dieselbe Tabelle → sonst kollidierende
/// Inverse-Navigations); geladen wird über <c>PersonAId == id || PersonBId == id</c>. FK mit
/// <c>Restrict</c> (kein Cascade-Pfad-Konflikt); Soft-Delete + Audit wie bei Doks.
/// </summary>
[Table("PersonBeziehungen")]
public class PersonRelation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string PersonAId { get; set; } = string.Empty;
    public Person? PersonA { get; set; }

    public string PersonBId { get; set; } = string.Empty;
    public Person? PersonB { get; set; }

    [Column("Typ")]
    public RelationType Type { get; set; }

    [Column("Notiz")]
    public string? Note { get; set; }

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
