using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>Per-year counter for sequential case numbers; incremented atomically via INSERT ... ON DUPLICATE KEY UPDATE.</summary>
[Table("AktenzeichenZaehler")]
public class CaseNumberCounter
{
    // composite PK part: P/F/G/…
    [Column("Praefix")]
    public string Prefix { get; set; } = "P";

    // composite PK part
    [Column("Jahr")]
    public int Year { get; set; }

    [Column("LetzteNummer")]
    public int LastNumber { get; set; }
}
