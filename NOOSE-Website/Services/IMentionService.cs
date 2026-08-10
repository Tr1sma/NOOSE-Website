using System.Security.Claims;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Resolves and searches @-mention tokens in stored text.</summary>
public interface IMentionService
{
    /// <summary>Resolves mention tokens in text; for a partner viewer pass their agency so unreleased targets are redacted.</summary>
    Task<IReadOnlyList<MentionSegment>> ResolveAsync(string? text, bool isLeadership, string? meId, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null);

    /// <summary>Batch-resolves mentions; preserves order. For a partner viewer pass their agency so unreleased targets are redacted.</summary>
    Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> ResolveManyAsync(IReadOnlyList<string?> texts, bool isLeadership, string? meId, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null);

    /// <summary>Candidates for @-picker autocomplete.</summary>
    /// <remarks>Takes the principal because the record half runs through the global search, whose gates are
    /// principal-shaped. The real-name half still reads <c>MayRealNameSee</c> separately from the classified flag.</remarks>
    Task<List<MentionHit>> CandidatesAsync(string? text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
