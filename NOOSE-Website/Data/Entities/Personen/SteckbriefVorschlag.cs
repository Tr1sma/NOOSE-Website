using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Ein gemeinsamer Vorschlagswert für ein Steckbrief-Mehrfachfeld (Waffe/Fahrzeug/Ort). Speist das
/// Autocomplete und wächst, sobald ein neuer Wert erfasst wird. Reine Referenzdaten – kein Audit und
/// kein Soft-Delete. Eindeutig je (<see cref="Typ"/>, <see cref="Wert"/>); die case-insensitive
/// DB-Collation behandelt „Karabiner" und „karabiner" als denselben Eintrag.
/// </summary>
public class SteckbriefVorschlag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public VorschlagTyp Typ { get; set; }
    public string Wert { get; set; } = string.Empty;
}
