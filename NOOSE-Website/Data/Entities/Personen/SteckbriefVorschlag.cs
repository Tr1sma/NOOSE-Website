using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Ein gemeinsamer Vorschlagswert für ein Steckbrief-Mehrfachfeld (Waffe/Fahrzeug/Ort). Speist das
/// Autocomplete und wächst, sobald ein neuer Wert erfasst wird. Reine Referenzdaten – kein Audit und
/// kein Soft-Delete. Eindeutig je (<see cref="Typ"/>, <see cref="Wert"/>); die case-insensitive
/// DB-Collation behandelt „Karabiner" und „karabiner" als denselben Eintrag.
/// </summary>
[Table("SteckbriefVorschlaege")]
public class SteckbriefVorschlag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("Typ")]
    public VorschlagTyp Typ { get; set; }
    [Column("Wert")]
    public string Wert { get; set; } = string.Empty;
}
