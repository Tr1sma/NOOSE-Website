namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Beendigung einer Maßnahme in einem Personen-Dok. „Erschossen" löst den temporären Tod der Person
/// aus; die „Amnestie-Spritze" lässt die Person leben (nur Gedächtnisverlust).
/// </summary>
public enum MeasureOutcome
{
    RunningStill = 0,
    OfficiallyReleased = 1,
    Injection = 2,
    Shot = 3,
}
