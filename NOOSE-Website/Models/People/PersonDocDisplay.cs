using NOOSE_Website.Data.Entities.People;

namespace NOOSE_Website.Models.People;

/// <summary>
/// Anzeigemodell eines Personen-Doks: das Dok selbst plus die – im Service aufgelösten und
/// Verschlusssache-gefilterten – Anzeigedaten der verknüpften Organisation. Ist keine Akte verknüpft
/// (oder für den Nutzer nicht sichtbar bzw. gelöscht), bleiben <see cref="OrgName"/>,
/// <see cref="OrgAktenzeichen"/> und <see cref="OrgRoute"/> null → die Anzeige fällt auf den
/// Freitext <see cref="PersonDok.Fraktion"/> zurück.
/// </summary>
/// <param name="Dok">Das zugrundeliegende Dok mit allen Feldern.</param>
/// <param name="OrgName">Name der verknüpften Fraktion/Gruppe oder null.</param>
/// <param name="OrgAktenzeichen">Aktenzeichen der verknüpften Organisation oder null.</param>
/// <param name="OrgRoute">Klickbares Ziel (<c>/fraktionen/{id}</c> bzw. <c>/personengruppen/{id}</c>) oder null.</param>
public record PersonDocDisplay(PersonDoc Doc, string? OrgName, string? OrgCaseNumber, string? OrgRoute);
