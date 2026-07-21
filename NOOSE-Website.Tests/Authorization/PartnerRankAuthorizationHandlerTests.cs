using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NOOSE_Website.Authorization;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Authorization;

public class PartnerRankAuthorizationHandlerTests
{
    private static async Task<bool> EvaluateAsync(
        ClaimsPrincipal user,
        PartnerAgency agency,
        PartnerRank minimum)
    {
        var requirement = new PartnerRankRequirement(agency, minimum);
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            user,
            resource: null);

        await new PartnerRankAuthorizationHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    // --- Requirement holds its constructor arguments ---

    [Fact]
    public void Requirement_exposes_agency_and_minimum()
    {
        var requirement = new PartnerRankRequirement(PartnerAgency.LSPD, PartnerRank.Special);

        Assert.Equal(PartnerAgency.LSPD, requirement.Agency);
        Assert.Equal(PartnerRank.Special, requirement.Minimum);
    }

    // --- Matching agency, rank at or above minimum -> succeeds ---

    [Theory]
    [InlineData(PartnerRank.Member, PartnerRank.Member)]   // exact threshold, base tier
    [InlineData(PartnerRank.Special, PartnerRank.Member)]  // above
    [InlineData(PartnerRank.Chief, PartnerRank.Member)]    // above
    [InlineData(PartnerRank.Special, PartnerRank.Special)] // exact threshold
    [InlineData(PartnerRank.Chief, PartnerRank.Special)]   // above
    [InlineData(PartnerRank.Chief, PartnerRank.Chief)]     // exact threshold, top tier
    public async Task HandleAsync_matching_agency_rank_at_or_above_minimum_succeeds(
        PartnerRank actual, PartnerRank minimum)
    {
        var user = ClaimsPrincipalBuilder.Agent().AsPartner(PartnerAgency.LSPD, actual);

        Assert.True(await EvaluateAsync(user, PartnerAgency.LSPD, minimum));
    }

    // --- Matching agency, rank below minimum -> fails ---

    [Theory]
    [InlineData(PartnerRank.Member, PartnerRank.Special)] // one below
    [InlineData(PartnerRank.Member, PartnerRank.Chief)]   // two below
    [InlineData(PartnerRank.Special, PartnerRank.Chief)]  // one below top
    public async Task HandleAsync_matching_agency_rank_below_minimum_fails(
        PartnerRank actual, PartnerRank minimum)
    {
        var user = ClaimsPrincipalBuilder.Agent().AsPartner(PartnerAgency.LSPD, actual);

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, minimum));
    }

    // --- Wrong agency -> fails even when rank is high enough ---

    [Theory]
    [InlineData(PartnerAgency.DoJ, PartnerAgency.LSPD)]
    [InlineData(PartnerAgency.LSPD, PartnerAgency.LSMD)]
    [InlineData(PartnerAgency.LSMD, PartnerAgency.DoJ)]
    public async Task HandleAsync_wrong_agency_fails(PartnerAgency actual, PartnerAgency required)
    {
        // top rank so only the agency mismatch can cause failure
        var user = ClaimsPrincipalBuilder.Agent().AsPartner(actual, PartnerRank.Chief);

        Assert.False(await EvaluateAsync(user, required, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_matching_agency_when_wrong_agency_would_fail_succeeds()
    {
        var user = ClaimsPrincipalBuilder.Agent().AsPartner(PartnerAgency.DoJ, PartnerRank.Member);

        Assert.True(await EvaluateAsync(user, PartnerAgency.DoJ, PartnerRank.Member));
    }

    // --- Non-partner principals -> fail ---

    [Fact]
    public async Task HandleAsync_internal_agent_without_partner_claims_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent().WithRank(Rank.Director);

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_admin_internal_agent_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent().AsAdmin().WithRank(Rank.Director);

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_anonymous_principal_fails()
    {
        var user = ClaimsPrincipalBuilder.Anonymous();

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    // --- Malformed / missing claims -> fail ---

    [Fact]
    public async Task HandleAsync_valid_agency_but_missing_rank_claim_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent()
            .Raw(AgentClaimTypes.PartnerAgency, ((int)PartnerAgency.LSPD).ToString());

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_valid_agency_but_nonnumeric_rank_claim_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent()
            .Raw(AgentClaimTypes.PartnerAgency, ((int)PartnerAgency.LSPD).ToString())
            .Raw(AgentClaimTypes.PartnerRank, "chief");

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_valid_agency_but_undefined_rank_value_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent()
            .Raw(AgentClaimTypes.PartnerAgency, ((int)PartnerAgency.LSPD).ToString())
            .Raw(AgentClaimTypes.PartnerRank, "99");

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_undefined_agency_value_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent()
            .Raw(AgentClaimTypes.PartnerAgency, "0")
            .Raw(AgentClaimTypes.PartnerRank, ((int)PartnerRank.Chief).ToString());

        Assert.False(await EvaluateAsync(user, PartnerAgency.DoJ, PartnerRank.Member));
    }

    [Fact]
    public async Task HandleAsync_rank_claim_but_missing_agency_claim_fails()
    {
        var user = ClaimsPrincipalBuilder.Agent()
            .Raw(AgentClaimTypes.PartnerRank, ((int)PartnerRank.Chief).ToString());

        Assert.False(await EvaluateAsync(user, PartnerAgency.LSPD, PartnerRank.Member));
    }

    // --- Handler does not mutate context on failure ---

    [Fact]
    public async Task HandleAsync_failure_leaves_context_not_succeeded()
    {
        var user = ClaimsPrincipalBuilder.Agent().AsPartner(PartnerAgency.DoJ, PartnerRank.Member);
        var requirement = new PartnerRankRequirement(PartnerAgency.LSPD, PartnerRank.Chief);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource: null);

        await new PartnerRankAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
