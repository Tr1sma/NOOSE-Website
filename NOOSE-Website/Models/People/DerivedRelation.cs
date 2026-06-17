using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>Computed (non-persisted) person-to-person relation derived from an org alliance/conflict.</summary>
public record DerivedRelation(
    LinkKind Kind,
    string PersonId,
    string PersonName,
    string CaseNumber,
    string SourceName,
    string PartnerName);
