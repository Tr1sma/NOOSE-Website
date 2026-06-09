namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Beendigung einer Maßnahme in einem Personen-Dok. „Erschossen" löst den temporären Tod der Person
/// aus; die „Amnestie-Spritze" lässt die Person leben (nur Gedächtnisverlust).
/// </summary>
public enum MassnahmeAusgang
{
    LaeuftNoch = 0,
    OffiziellEntlassen = 1,
    Spritze = 2,
    Erschossen = 3,
}
