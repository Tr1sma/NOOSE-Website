using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The gateway is the only path to the model: it pre-checks the quota, bills exactly once and always logs.</summary>
public sealed class NooseiGatewayTests
{
    private const string AgentId = "agent-gw";

    private static ClaimsPrincipal Agent(Rank rank = Rank.SpecialAgent)
        => ClaimsPrincipalBuilder.Agent(AgentId).WithRank(rank).WithCodename("Falke").Build();

    private static LlmOptions Tuning(Action<LlmOptions>? configure = null)
    {
        var o = new LlmOptions
        {
            Enabled = true,
            ApiKey = "k",
            Model = "vendor/model",
            TurnTimeoutSeconds = 30,
            MaxToolRounds = 3,
            MaxToolCallsPerRound = 2,
            ToolTimeoutSeconds = 5,
        };
        configure?.Invoke(o);
        return o;
    }

    private static LlmResult Result(string? text = "Antwort", decimal cost = 0.01m, params LlmToolCall[] calls)
        => new(text, calls, new LlmUsage(100, 20, 120, 0, 0, cost), "Baidu", "vendor/model", "stop", "gen-1", 1, 42);

    private static async Task<(NooseiGateway Gateway, ILlmService Llm, LlmQuotaService Quota)> BuildAsync(
        SqliteTestContext ctx, Rank rank = Rank.SpecialAgent, long? over = null, Action<LlmOptions>? configure = null)
    {
        await using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent(AgentId, rank, configure: a =>
            {
                a.Rank = rank;
                a.LlmQuotaOverride = over;
                a.Codename = "Falke";
            }));
            await db.SaveChangesAsync();
        }

        var configService = Substitute.For<ILlmQuotaConfigService>();
        configService.GetAsync(Arg.Any<CancellationToken>()).Returns(LlmQuotaConfig.Default());
        var quota = new LlmQuotaService(ctx.Factory, configService);

        var llm = Substitute.For<ILlmService>();
        llm.IsConfigured.Returns(true);

        var gateway = new NooseiGateway(llm, quota, Options.Create(Tuning(configure)),
            NullLogger<NooseiGateway>.Instance);
        return (gateway, llm, quota);
    }

    private static NooseiCall Call(
        IReadOnlyList<LlmToolDefinition>? tools = null, NooseiToolExecutor? executor = null)
        => new(LlmFeature.Chat, [LlmMessage.System("sys"), LlmMessage.User("Wer führt die Ballas?")],
            LoggedPrompt: "Wer führt die Ballas?", Tools: tools, ToolExecutor: executor);

    private static LlmToolDefinition Tool(string name = "suche_akten")
        => new(name, "Sucht Akten.", System.Text.Json.JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone());

    // ---- metering ----

    [Fact]
    public async Task Ask_ChargesTheRealCost_AfterTheAnswer()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result(cost: 0.0123m));

        var answer = await gateway.AskAsync(Call(), Agent());

        Assert.Equal("Antwort", answer.Text);
        Assert.Equal(1_230L, answer.Charge.QuotaTokens);
        Assert.Equal(35_000L - 1_230L, answer.Charge.Status.Remaining);
    }

    [Fact]
    public async Task Ask_WritesTheLogRow_WithPromptAnswerAndReferences()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result());

        await gateway.AskAsync(Call() with { ContextRefs = [new LlmContextRef("Faction", "f1", "Ballas")] }, Agent());

        await using var db = ctx.NewContext();
        var row = Assert.Single(await db.LlmRequests.ToListAsync());
        Assert.Equal("Wer führt die Ballas?", row.Prompt);
        Assert.Equal("Antwort", row.Answer);
        Assert.Contains("Ballas", row.ContextRefsJson);
        Assert.True(row.Success);
    }

    [Fact]
    public async Task Ask_BlocksWhenExhausted_AndNeverCallsTheEndpoint()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx, over: 0);

        await Assert.ThrowsAsync<LlmQuotaExceededException>(() => gateway.AskAsync(Call(), Agent()));

        await llm.DidNotReceive().CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
        await using var db = ctx.NewContext();
        Assert.Empty(await db.LlmRequests.ToListAsync());
    }

    [Fact]
    public async Task Ask_AllowsAtOneTokenLeft_AndMayEndNegative()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx, over: 1);
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result(cost: 0.05m));

        var answer = await gateway.AskAsync(Call(), Agent());

        Assert.Equal(5_000L, answer.Charge.QuotaTokens);
        Assert.Equal(1L - 5_000L, answer.Charge.Status.Remaining);
        Assert.True(answer.Charge.Status.IsBlocked);
    }

    [Fact]
    public async Task Ask_LogsAFailedCall_WithZeroTokensAndTheError()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns<LlmResult>(_ => throw new InvalidOperationException("NOOSEI antwortete nicht (Fehler 502)."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.AskAsync(Call(), Agent()));

        await using var db = ctx.NewContext();
        var row = Assert.Single(await db.LlmRequests.ToListAsync());
        Assert.False(row.Success);
        Assert.Equal(0L, row.QuotaTokens);
        Assert.Contains("502", row.ErrorMessage);
        Assert.Null(row.Answer);
    }

    [Theory]
    [InlineData("partner")]
    [InlineData("demo")]
    [InlineData("supervision")]
    public async Task Ask_DeniesTheRolesWithoutNooseiAccess(string role)
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        var builder = ClaimsPrincipalBuilder.Agent(AgentId).WithRank(Rank.SpecialAgent);
        var actor = role switch
        {
            "partner" => builder.AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build(),
            "demo" => builder.AsDemo().Build(),
            _ => builder.AsTeamLead().Build(),
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => gateway.AskAsync(Call(), actor));
        await llm.DidNotReceive().CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    // ---- tool loop ----

    [Fact]
    public async Task Ask_RunsToolsAndBillsEveryRoundAsOneRequest()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        var round = 0;
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++round == 1
                ? Result(null, 0.01m, new LlmToolCall("c1", "suche_akten", "{}"))
                : Result("Fertig", 0.01m));

        var answer = await gateway.AskAsync(
            Call([Tool()], (_, _) => Task.FromResult(NooseiToolOutcome.Plain("• Person | Max Mustermann"))), Agent());

        Assert.Equal("Fertig", answer.Text);
        Assert.Equal(2, answer.Rounds);
        Assert.Equal(2_000L, answer.Charge.QuotaTokens);

        await using var db = ctx.NewContext();
        var row = Assert.Single(await db.LlmRequests.ToListAsync());
        Assert.Equal(1, row.ToolRounds);
        Assert.Contains("suche_akten", row.ContextRefsJson);
    }

    [Fact]
    public async Task Ask_WithdrawsToolsOnTheLastRound_SoTheModelAlwaysAnswers()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx, configure: o => o.MaxToolRounds = 2);
        var seenWithoutTools = false;
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<LlmRequest>();
                if (!request.OffersTools)
                {
                    seenWithoutTools = true;
                    return Result("Endlich", 0.001m);
                }
                return Result(null, 0.001m, new LlmToolCall("c", "suche_akten", "{}"));
            });

        var answer = await gateway.AskAsync(
            Call([Tool()], (_, _) => Task.FromResult(NooseiToolOutcome.Plain("nichts gefunden"))), Agent());

        Assert.True(seenWithoutTools);
        Assert.Equal("Endlich", answer.Text);
        Assert.Equal(3, answer.Rounds);
    }

    [Fact]
    public async Task Ask_HandsTheModelAGermanErrorWhenAToolFails()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        var toolReply = string.Empty;
        var round = 0;
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<LlmRequest>();
                if (++round == 1)
                {
                    return Result(null, 0.001m, new LlmToolCall("c", "suche_akten", "{}"));
                }
                toolReply = request.Messages.Last(m => m.Role == LlmRole.Tool).Content ?? string.Empty;
                return Result("Trotzdem geantwortet", 0.001m);
            });

        var answer = await gateway.AskAsync(
            Call([Tool()], (_, _) => throw new InvalidOperationException("kaputt")), Agent());

        Assert.Equal("Trotzdem geantwortet", answer.Text);
        Assert.Contains("Werkzeug konnte nicht ausgeführt werden", toolReply);
    }

    [Fact]
    public async Task Ask_AnswersWithoutFileAccess_WhenTheEndpointRefusesTools()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<LlmRequest>().OffersTools
                ? throw new LlmCapabilityException(schemaRelated: false, toolsRelated: true)
                : Result("Ohne Akten", 0.001m));

        var answer = await gateway.AskAsync(
            Call([Tool()], (_, _) => Task.FromResult(NooseiToolOutcome.Plain("nie erreicht"))), Agent());

        Assert.True(answer.Degraded);
        Assert.Equal("Ohne Akten", answer.Text);
    }

    // ---- what a turn touched ----

    [Fact]
    public async Task Ask_CarriesTheRecordsATooltouched_IntoTheAnswerAndTheLog()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        var round = 0;
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++round == 1
                ? Result(null, 0.01m, new LlmToolCall("c1", "lies_akte", "{}"))
                : Result("Fertig", 0.01m));

        var answer = await gateway.AskAsync(
            Call([Tool("lies_akte")], (_, _) => Task.FromResult(
                new NooseiToolOutcome("Akte …", [new LlmContextRef("Person", "p1", "Max Mustermann")]))),
            Agent());

        // without this the source chips have nothing to render and the log names only the tool
        var record = Assert.Single(answer.Refs!, r => r.Id is not null);
        Assert.Equal("Max Mustermann", record.Name);

        await using var db = ctx.NewContext();
        var row = Assert.Single(await db.LlmRequests.ToListAsync());
        Assert.Contains("Max Mustermann", row.ContextRefsJson);
        Assert.Contains("lies_akte", row.ContextRefsJson);
    }

    [Fact]
    public async Task Ask_ReportsNoRecords_WhenNoToolWasCalled()
    {
        using var ctx = new SqliteTestContext();
        var (gateway, llm, _) = await BuildAsync(ctx);
        llm.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(Result("Direkt"));

        var answer = await gateway.AskAsync(Call(), Agent());

        Assert.Empty(answer.Refs!);
    }
}
