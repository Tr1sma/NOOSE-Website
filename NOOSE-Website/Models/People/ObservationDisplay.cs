using NOOSE_Website.Data.Entities.People;

namespace NOOSE_Website.Models.People;

/// <summary>
/// Anzeigemodell einer Observation: der Eintrag selbst plus die – im Service aufgelösten und
/// Verschlusssache-gefilterten – Anzeigedaten. <see cref="AgentCodename"/> ist der Deckname des
/// beobachtenden Agents (oder null). Ist eine Fraktion/Gruppe verknüpft und für den Nutzer sichtbar,
/// liefern <see cref="OrgName"/>, <see cref="OrgAktenzeichen"/> und <see cref="OrgRoute"/> die Anzeige-
/// und Sprungdaten; sonst bleiben sie null.
/// </summary>
/// <param name="Obs">Die zugrundeliegende Observation mit allen Feldern.</param>
/// <param name="AgentCodename">Deckname des beobachtenden Agents oder null.</param>
/// <param name="OrgName">Name der verknüpften Fraktion/Gruppe oder null.</param>
/// <param name="OrgAktenzeichen">Aktenzeichen der verknüpften Organisation oder null.</param>
/// <param name="OrgRoute">Klickbares Ziel (<c>/fraktionen/{id}</c> bzw. <c>/personengruppen/{id}</c>) oder null.</param>
public record ObservationDisplay(Observation Obs, string? AgentCodename, string? OrgName, string? OrgCaseNumber, string? OrgRoute);
