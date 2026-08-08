using System.Security.Claims;
using NOOSE_Website.Data.Entities.Llm;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The weekly bill across every agent. It is money, so who may read it matters as much as the sums.</summary>
public sealed class LlmWeeklySpendTests
{
    private static ClaimsPrincipal Owner()
        => ClaimsPrincipalBuilder.Agent("owner").WithRank(Rank.Director).AsAdmin().AsAiOwner().Build();

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).AsAdmin().Build();

    private static LlmRequestLogService Service(SqliteTestContext ctx)
    {
        var config = Substitute.For<ILlmQuotaConfigService>();
        config.GetAsync(Arg.Any<CancellationToken>()).Returns(LlmQuotaConfig.Default());
        return new LlmRequestLogService(ctx.Factory, new LlmQuotaService(ctx.Factory, config));
    }

    private static LlmRequestLog Row(string agentId, int year, int week, long tokens, decimal cost)
        => new()
        {
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow,
            BudgetYear = year,
            BudgetWeek = week,
            Feature = LlmFeature.Chat,
            QuotaTokens = tokens,
            CostUsd = cost,
            Success = true,
        };

    [Fact]
    public async Task Spend_IsRefusedToAnAdminWhoIsNotTheAiOwner()
    {
        using var ctx = new SqliteTestContext();

        // leadership runs the quota in tokens; the bill belongs to whoever pays it
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Service(ctx).GetWeeklySpendAsync(Leader()));
    }

    [Fact]
    public async Task Spend_SumsEveryAgentIntoOneRowPerWeek()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.LlmRequests.Add(Row("a", 2026, 30, 1_000, 0.01m));
            db.LlmRequests.Add(Row("b", 2026, 30, 4_000, 0.04m));
            db.LlmRequests.Add(Row("a", 2026, 31, 2_000, 0.02m));
            await db.SaveChangesAsync();
        }

        var weeks = await Service(ctx).GetWeeklySpendAsync(Owner());

        Assert.Equal(2, weeks.Count);
        var thirty = weeks.Single(w => w.Week == 30);
        Assert.Equal(5_000, thirty.QuotaTokens);
        Assert.Equal(0.05m, thirty.CostUsd);
    }

    [Fact]
    public async Task Spend_ReturnsOldestFirst()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.LlmRequests.Add(Row("a", 2026, 31, 1_000, 0.01m));
            db.LlmRequests.Add(Row("a", 2026, 29, 1_000, 0.01m));
            await db.SaveChangesAsync();
        }

        var weeks = await Service(ctx).GetWeeklySpendAsync(Owner());

        Assert.Equal([29, 31], weeks.Select(w => w.Week));
    }

    [Fact]
    public async Task Spend_MarksTheRunningWeek_SoTheForecastCanSkipIt()
    {
        var (year, week) = IsoWeekPeriod.Current();
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.LlmRequests.Add(Row("a", year, week, 1_000, 0.01m));
            db.LlmRequests.Add(Row("a", 2020, 5, 9_000, 0.09m));
            await db.SaveChangesAsync();
        }

        var weeks = await Service(ctx).GetWeeklySpendAsync(Owner());

        Assert.True(weeks.Single(w => w.Year == year && w.Week == week).Running);
        Assert.False(weeks.Single(w => w.Year == 2020).Running);
        // and the forecast then rests on the closed week alone
        Assert.Equal(0.09m, LlmCostForecast.Expected(weeks)!.Value.CostUsd);
    }

    [Fact]
    public async Task Spend_IsEmptyWithoutAnyRequests()
        => Assert.Empty(await Service(new SqliteTestContext()).GetWeeklySpendAsync(Owner()));
}
