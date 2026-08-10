using System.Security.Claims;

namespace NOOSE_Website.Services.Search;

/// <summary>Everything a provider needs to know about who is asking, resolved once per search.</summary>
/// <param name="User">The principal. Carried because three canonical gates are principal-shaped —
/// <see cref="InformantVisibility"/>, <c>MayRealNameSee</c> and <c>MayCounterIntel</c> — and a provider that
/// re-derives them from the scope would be writing a fourth copy of a rule that already exists.</param>
/// <param name="PartnerAllowedTypes">Rank allowlist of an external partner; null = no restriction. Resolved once
/// here rather than per provider, and applied to search for the first time: it used to gate navigation only, so a
/// partner could find by search what their rank was not allowed to list.</param>
/// <param name="NowUtc">Injected so the meeting agenda's time gate is testable without waiting two hours.</param>
public sealed record SearchViewer(
    ClaimsPrincipal User,
    ViewerScope Scope,
    IReadOnlySet<string>? PartnerAllowedTypes,
    DateTime NowUtc)
{
    public bool IsPartner => Scope.IsPartner;

    public string? MeId => Scope.MeId;

    /// <summary>Builds from the principal, resolving the partner rank allowlist.</summary>
    public static async Task<SearchViewer> FromAsync(
        ClaimsPrincipal user, IPartnerVisibilityPolicyService partnerPolicy, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(user);
        // internal viewers get null back from the policy, so this costs a partner one cached lookup and nobody else
        var allowed = scope.IsPartner ? await partnerPolicy.GetAllowedTypesAsync(user, cancellationToken) : null;
        return new SearchViewer(user, scope, allowed, DateTime.UtcNow);
    }
}
