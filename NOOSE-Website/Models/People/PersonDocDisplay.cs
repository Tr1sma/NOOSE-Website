using NOOSE_Website.Data.Entities.People;

namespace NOOSE_Website.Models.People;

/// <summary>Display model for a person doc with resolved, classification-filtered org data; null org falls back to free-text faction.</summary>
public record PersonDocDisplay(PersonDoc Doc, string? OrgName, string? OrgCaseNumber, string? OrgRoute);
