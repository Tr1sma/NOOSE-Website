using NOOSE_Website.Data.Entities.Financing;

namespace NOOSE_Website.Models.Financing;

/// <summary>A funding request plus the requester's codename, so lists need no extra lookup.</summary>
public record FinancingRequestDisplay(FinancingRequest Request, string AgentCodename);
