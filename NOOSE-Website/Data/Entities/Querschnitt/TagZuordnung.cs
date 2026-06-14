using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Verknüpft ein <see cref="Tag"/> mit einer beliebigen Akte (polymorph über Typ + Id).
/// Einfache Zuordnungs-Zeile ohne Audit/Soft-Delete – wird beim Ent-Taggen hart entfernt.
/// </summary>
[Table("TagZuordnungen")]
public class TagZuordnung
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TagId { get; set; } = string.Empty;
    public Tag? Tag { get; set; }

    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;
    [Column("EntitaetId")]
    public string EntitaetId { get; set; } = string.Empty;
}
