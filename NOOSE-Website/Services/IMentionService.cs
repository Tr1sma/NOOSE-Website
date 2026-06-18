using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Resolves and searches @-mention tokens in stored text.</summary>
public interface IMentionService
{
    /// <summary>Resolves mention tokens in text.</summary>
    Task<IReadOnlyList<MentionSegment>> ResolveAsync(string? text, bool isLeadership, string? meId, CancellationToken cancellationToken = default);

    /// <summary>Batch-resolves mentions; preserves order.</summary>
    Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> ResolveManyAsync(IReadOnlyList<string?> texts, bool isLeadership, string? meId, CancellationToken cancellationToken = default);

    /// <summary>Candidates for @-picker autocomplete.</summary>
    Task<List<MentionHit>> CandidatesAsync(string? text, bool mayClassifiedRead, bool mayRealName, string? meId, CancellationToken cancellationToken = default);
}
