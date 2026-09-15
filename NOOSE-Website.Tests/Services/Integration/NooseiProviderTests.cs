using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The upstream switch: who may flip it, what it may be flipped to, and what the boost does to a quota.</summary>
public sealed class NooseiProviderTests
{
    private const string SettingKey = "KiAnbieter";

    private static ClaimsPrincipal Owner()
        => ClaimsPrincipalBuilder.Agent("owner").WithRank(Rank.Director).AsAiOwner().Build();

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    /// <summary>Both upstreams reachable unless a test says otherwise.</summary>
    private static LlmOptions Reachable(Action<LlmOptions>? configure = null)
    {
        var o = new LlmOptions
        {
            Enabled = true,
            ApiKey = "router-key",
            Model = "vendor/model",
            DeepSeek = { ApiKey = "deepseek-key" },
        };
        configure?.Invoke(o);
        return o;
    }

    private static NooseiProviderService Build(SqliteTestContext ctx, LlmOptions? options = null, IMemoryCache? cache = null)
        => new(ctx.Factory, cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options ?? Reachable()));

    private static async Task StoreAsync(SqliteTestContext ctx, string? raw)
    {
        await using var db = ctx.NewContext();
        db.SystemSettings.Add(new SystemSetting { Key = SettingKey, Value = raw });
        await db.SaveChangesAsync();
    }

    // ---- resolution ----

    [Fact]
    public async Task NoRow_TakesTheDeploymentsOwnDefault()
    {
        using var ctx = new SqliteTestContext();

        var state = await Build(ctx, Reachable(o => o.DefaultProvider = LlmProvider.DeepSeek)).GetStateAsync();

        Assert.Equal(LlmProvider.DeepSeek, state.Active);
        Assert.Equal(0, state.BoostPercent);
    }

    [Fact]
    public async Task StoredChoice_Wins()
    {
        using var ctx = new SqliteTestContext();
        await Build(ctx).SaveAsync(new LlmProviderSettings { Active = LlmProvider.DeepSeek }, Owner());

        var state = await Build(ctx).GetStateAsync();

        Assert.Equal(LlmProvider.DeepSeek, state.Active);
    }

    /// <summary>A key pulled out of the environment must not fail every agent's next question.</summary>
    [Fact]
    public async Task StoredChoiceWithoutAKey_FallsBackInsteadOfBreaking()
    {
        using var ctx = new SqliteTestContext();
        await StoreAsync(ctx, """{"Active":1}""");

        var state = await Build(ctx, Reachable(o => o.DeepSeek.ApiKey = string.Empty)).GetStateAsync();

        Assert.Equal(LlmProvider.OpenRouter, state.Active);
    }

    [Fact]
    public async Task UnreadableRow_FallsBackInsteadOfThrowing()
    {
        using var ctx = new SqliteTestContext();
        await StoreAsync(ctx, "kein json");

        var state = await Build(ctx).GetStateAsync();

        Assert.Equal(LlmProvider.OpenRouter, state.Active);
    }

    /// <summary>The boost belongs to an upstream, so only the active one's counts.</summary>
    [Fact]
    public async Task OnlyTheActiveUpstreamsBoostApplies()
    {
        using var ctx = new SqliteTestContext();
        var settings = new LlmProviderSettings { Active = LlmProvider.DeepSeek };
        settings.SetBoost(LlmProvider.OpenRouter, 10);
        settings.SetBoost(LlmProvider.DeepSeek, 200);
        await Build(ctx).SaveAsync(settings, Owner());

        Assert.Equal(200, (await Build(ctx).GetStateAsync()).BoostPercent);

        await Build(ctx).SaveAsync(new LlmProviderSettings
        {
            Active = LlmProvider.OpenRouter,
            BoostPercent = settings.BoostPercent,
        }, Owner());

        Assert.Equal(10, (await Build(ctx).GetStateAsync()).BoostPercent);
    }

    // ---- write gate ----

    [Fact]
    public async Task OnlyTheAiOwnerMaySwitch()
    {
        using var ctx = new SqliteTestContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(ctx).SaveAsync(new LlmProviderSettings { Active = LlmProvider.DeepSeek }, Leader()));
    }

    [Fact]
    public async Task ReadOnlySupervision_MayNotSwitch()
    {
        using var ctx = new SqliteTestContext();
        var supervision = ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().AsAiOwner().Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Build(ctx).SaveAsync(new LlmProviderSettings { Active = LlmProvider.DeepSeek }, supervision));
    }

    [Fact]
    public async Task AnUpstreamWithoutAKey_CannotBeSelected()
    {
        using var ctx = new SqliteTestContext();
        var options = Reachable(o => o.DeepSeek.ApiKey = string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Build(ctx, options).SaveAsync(new LlmProviderSettings { Active = LlmProvider.DeepSeek }, Owner()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(LlmProviderSettings.MaxBoostPercent + 1)]
    public async Task ABoostOutsideTheRange_IsRefused(int percent)
    {
        using var ctx = new SqliteTestContext();
        var settings = new LlmProviderSettings();
        settings.BoostPercent[LlmProviderSettings.ProviderKey(LlmProvider.DeepSeek)] = percent;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build(ctx).SaveAsync(settings, Owner()));
    }

    [Fact]
    public async Task SwitchingWritesAnAuditRow()
    {
        using var ctx = new SqliteTestContext();

        await Build(ctx).SaveAsync(new LlmProviderSettings { Active = LlmProvider.DeepSeek }, Owner());

        await using var db = ctx.NewContext();
        var row = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityType == NooseiProviderService.AuditType);
        Assert.NotNull(row);
    }

    // ---- options plumbing ----

    [Fact]
    public void EachUpstreamKeepsItsOwnAddressKeyAndModel()
    {
        var o = Reachable(x =>
        {
            x.BaseUrl = "https://openrouter.test/api/v1";
            x.DeepSeek.BaseUrl = "https://deepseek.test/v1";
            x.DeepSeek.Model = "deepseek-flash";
        });

        Assert.Equal("https://openrouter.test/api/v1", o.BaseUrlFor(LlmProvider.OpenRouter));
        Assert.Equal("https://deepseek.test/v1", o.BaseUrlFor(LlmProvider.DeepSeek));
        Assert.Equal("router-key", o.ApiKeyFor(LlmProvider.OpenRouter));
        Assert.Equal("deepseek-key", o.ApiKeyFor(LlmProvider.DeepSeek));
        Assert.Equal("vendor/model", o.ModelFor(LlmProvider.OpenRouter, LlmFeature.Chat));
        Assert.Equal("deepseek-flash", o.ModelFor(LlmProvider.DeepSeek, LlmFeature.Chat));
    }

    /// <summary>A per-feature override must not leak across upstreams — the ids are not portable.</summary>
    [Fact]
    public void APerFeatureOverrideStaysWithItsUpstream()
    {
        var o = Reachable(x =>
        {
            x.ModelByFeature[LlmFeature.Proofread] = "vendor/cheap";
            x.DeepSeek.Model = "deepseek-flash";
        });

        Assert.Equal("vendor/cheap", o.ModelFor(LlmProvider.OpenRouter, LlmFeature.Proofread));
        Assert.Equal("deepseek-flash", o.ModelFor(LlmProvider.DeepSeek, LlmFeature.Proofread));
    }

    /// <summary>DeepSeek reports no cost, so a model without a price would meter as good as free.</summary>
    [Fact]
    public void TheShippedDeepSeekModelCarriesAPrice()
    {
        var price = new LlmOptions().PriceFor("deepseek-flash");

        Assert.NotNull(price);
        Assert.True(price!.InputPerMillionUsd > 0m);
        Assert.True(price.OutputPerMillionUsd > 0m);
        Assert.True(LlmQuotaMath.FromCost(0m, 100_000, 20_000, price) > 0L);
    }

    [Fact]
    public void IsConfigured_IsTrue_AsSoonAsOneUpstreamIsUsable()
    {
        Assert.True(Reachable(o => o.ApiKey = string.Empty).IsConfigured);
        Assert.True(Reachable(o => o.DeepSeek.ApiKey = string.Empty).IsConfigured);
        Assert.False(Reachable(o =>
        {
            o.ApiKey = string.Empty;
            o.DeepSeek.ApiKey = string.Empty;
        }).IsConfigured);
        Assert.False(Reachable(o => o.Enabled = false).IsConfigured);
    }
}
