namespace NOOSE_Website.Models.People;

/// <summary>A person's membership in a parent record (back-link); EndedAt null means still active.</summary>
public record PersonAffiliation(string Type, string MemberId, string Id, string Name, string CaseNumber,
    string? Role, bool IsLead, DateTime JoinedAt, DateTime? EndedAt);
