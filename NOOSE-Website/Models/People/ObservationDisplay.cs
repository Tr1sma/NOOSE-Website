using NOOSE_Website.Data.Entities.People;

namespace NOOSE_Website.Models.People;

/// <summary>Display model for an observation with resolved, classification-filtered org data.</summary>
public record ObservationDisplay(Observation Obs, string? AgentCodename, string? OrgName, string? OrgCaseNumber, string? OrgRoute);
