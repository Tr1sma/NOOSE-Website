using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>
/// Eine abgeleitete (nicht gespeicherte) Beziehung einer Person zu einer anderen Person, die sich aus
/// einem Bündnis/Konflikt zwischen Organisationen ergibt: Ist die Person Mitglied einer Organisation
/// (<paramref name="QuelleName"/>), die mit einer anderen Organisation (<paramref name="PartnerName"/>)
/// verbündet/verfeindet ist, so ist deren Mitglied <paramref name="PersonName"/> ein abgeleiteter
/// Verbündeter (<see cref="VerknuepfungArt.Buendnis"/>) bzw. Gegner (<see cref="VerknuepfungArt.Konflikt"/>).
/// Wird beim Anzeigen berechnet – immer aktuell, nicht manuell pflegbar.
/// </summary>
public record DerivedRelation(
    LinkKind Kind,
    string PersonId,
    string PersonName,
    string CaseNumber,
    string SourceName,
    string PartnerName);
