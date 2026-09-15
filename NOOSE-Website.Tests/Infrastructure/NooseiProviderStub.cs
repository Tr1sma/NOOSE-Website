using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Infrastructure;

/// <summary>Stubbed upstream selection. Everything that is not about the provider switch itself runs on
/// OpenRouter with no quota boost — the shape every existing test was written against.</summary>
public static class NooseiProviderStub
{
    public static INooseiProviderService Returning(
        LlmProvider active = LlmProvider.OpenRouter, int boostPercent = 0)
    {
        var service = Substitute.For<INooseiProviderService>();
        service.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(new LlmProviderState(active, boostPercent));
        return service;
    }
}
