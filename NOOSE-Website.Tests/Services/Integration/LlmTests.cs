using System.Net.Http;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Tests for the AI assistant plumbing: prompt guard, use guard, and config gating (no live endpoint).</summary>
public sealed class LlmTests
{
    private static ClaimsPrincipal Agent() => ClaimsPrincipalBuilder.Agent("a").WithRank(Rank.SpecialAgent).Build();

    private static LlmService Service(LlmOptions options)
        => new(Substitute.For<IHttpClientFactory>(), Options.Create(options));

    // ---- PromptRedactor ----

    [Fact]
    public void Clip_TruncatesLongText()
    {
        var text = new string('x', PromptRedactor.MaxContextChars + 500);
        var result = PromptRedactor.Clip(text);
        Assert.True(result.Length <= PromptRedactor.MaxContextChars + 10);
        Assert.EndsWith("[…]", result);
    }

    [Fact]
    public void Clip_Empty_ReturnsEmpty() => Assert.Equal(string.Empty, PromptRedactor.Clip(null));

    [Fact]
    public void GuardClassified_Throws_WhenNotAllowed()
        => Assert.Throws<InvalidOperationException>(() => PromptRedactor.GuardClassified(true, new LlmOptions { AllowClassifiedContent = false }));

    [Fact]
    public void GuardClassified_Ok_WhenAllowedOrNotClassified()
    {
        PromptRedactor.GuardClassified(true, new LlmOptions { AllowClassifiedContent = true });
        PromptRedactor.GuardClassified(false, new LlmOptions());
    }

    // ---- RequireLlmUse ----

    [Fact]
    public void RequireLlmUse_Throws_ForPartner()
        => Assert.Throws<UnauthorizedAccessException>(() =>
            Permission.RequireLlmUse(ClaimsPrincipalBuilder.Agent("p").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build()));

    [Fact]
    public void RequireLlmUse_Throws_ForDemo()
        => Assert.Throws<UnauthorizedAccessException>(() =>
            Permission.RequireLlmUse(ClaimsPrincipalBuilder.Agent("d").AsDemo().Build()));

    [Fact]
    public void RequireLlmUse_Ok_ForAgent() => Permission.RequireLlmUse(Agent());

    // ---- LlmService config gating ----

    [Fact]
    public void IsConfigured_False_WhenDisabled()
        => Assert.False(Service(new LlmOptions { Enabled = false, ApiKey = "k", Model = "m" }).IsConfigured);

    [Fact]
    public void IsConfigured_False_WhenNoKey()
        => Assert.False(Service(new LlmOptions { Enabled = true, ApiKey = "", Model = "m" }).IsConfigured);

    [Fact]
    public void IsConfigured_True_WhenComplete()
    {
        var svc = Service(new LlmOptions { Enabled = true, ApiKey = "k", Model = "deepseek/deepseek-v4-flash" });
        Assert.True(svc.IsConfigured);
        Assert.Equal("deepseek/deepseek-v4-flash", svc.Model);
    }

    [Fact]
    public async Task ChatAsync_Throws_WhenNotConfigured()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Service(new LlmOptions()).ChatAsync("s", "u", Agent()));

    [Fact]
    public async Task ChatAsync_Throws_ForPartner_BeforeAnyCall()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service(new LlmOptions { Enabled = true, ApiKey = "k", Model = "m" })
                .ChatAsync("s", "u", ClaimsPrincipalBuilder.Agent("p").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build()));
}
