using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Llm.Tools;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>NOOSEI conversations: owner-private, replayed with history, and tool results dropped after a rights change.</summary>
public sealed class NooseiChatServiceTests
{
    private const string Owner = "agent-owner";
    private const string Other = "agent-other";

    private static ClaimsPrincipal Actor(string id = Owner, Rank rank = Rank.SpecialAgent)
        => ClaimsPrincipalBuilder.Agent(id).WithRank(rank).Build();

    private static NooseiAnswer Answer(string text = "Antwort", IReadOnlyList<LlmMessage>? transcript = null)
        => new(text, LlmUsage.Empty, new LlmQuotaCharge(120, 0.0012m, LlmQuotaStatus.Empty, null, true),
            1, transcript ?? [], false);

    private static (NooseiChatService Svc, INooseiGateway Gateway) Build(
        SqliteTestContext ctx, Func<NooseiCall, NooseiAnswer>? respond = null)
    {
        var gateway = Substitute.For<INooseiGateway>();
        gateway.IsConfigured.Returns(true);
        gateway.AskAsync(Arg.Any<NooseiCall>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(call => respond is null ? Answer() : respond(call.Arg<NooseiCall>()));

        var settings = Substitute.For<INooseiSettingsService>();
        settings.GetAddendumAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

        var registry = new NooseiToolRegistry([new ResolveMentionTool(Substitute.For<IMentionService>())]);

        return (new NooseiChatService(ctx.Factory, gateway, settings, registry, NullLogger<NooseiChatService>.Instance), gateway);
    }

    private static async Task SeedAgentsAsync(SqliteTestContext ctx)
    {
        await using var db = ctx.NewContext();
        db.Users.Add(Seed.Agent(Owner, Rank.SpecialAgent, configure: a => a.Rank = Rank.SpecialAgent));
        db.Users.Add(Seed.Agent(Other, Rank.SpecialAgent, configure: a => a.Rank = Rank.SpecialAgent));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Ask_CreatesTheConversation_AndStoresBothTurns()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);

        var turn = await svc.AskAsync(null, "Wer führt die Ballas?", Actor());

        Assert.False(string.IsNullOrEmpty(turn.ConversationId));
        Assert.Equal("Antwort", turn.Answer.Text);
        Assert.Equal(120L, turn.QuotaTokens);

        var messages = await svc.GetMessagesAsync(turn.ConversationId, Actor());
        Assert.Equal(2, messages.Count);
        Assert.True(messages[0].FromUser);
        Assert.False(messages[1].FromUser);
    }

    [Fact]
    public async Task Ask_RendersTheAnswersMarkdownToHtml()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx, _ => Answer("**Ballas** führt:\n\n- Max Mustermann\n- Erika Muster"));

        var turn = await svc.AskAsync(null, "Wer führt die Ballas?", Actor());

        Assert.Contains("<strong>Ballas</strong>", turn.Answer.Html);
        Assert.Contains("<li>", turn.Answer.Html);
        // the raw Markdown stays available, and the DB keeps it too
        Assert.Contains("**Ballas**", turn.Answer.Text);
    }

    [Fact]
    public async Task Messages_ComeBackRenderedFromStorage()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx, _ => Answer("## Lage\n\nAlles *ruhig*."));

        var turn = await svc.AskAsync(null, "Wie ist die Lage?", Actor());
        var messages = await svc.GetMessagesAsync(turn.ConversationId, Actor());

        var answer = Assert.Single(messages, m => !m.FromUser);
        Assert.Contains("<h2", answer.Html);
        Assert.Contains("<em>ruhig</em>", answer.Html);
    }

    [Fact]
    public async Task OwnTurns_StayVerbatim_AndAreNeverTreatedAsMarkdown()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);

        var turn = await svc.AskAsync(null, "Was bedeutet **das** hier?", Actor());
        var messages = await svc.GetMessagesAsync(turn.ConversationId, Actor());

        var question = Assert.Single(messages, m => m.FromUser);
        Assert.Null(question.Html);
        Assert.Equal("Was bedeutet **das** hier?", question.Text);
    }

    [Fact]
    public async Task RenderedAnswer_NeutralisesRawHtmlFromTheModel()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx, _ => Answer("Text <script>alert(1)</script> und <img src=x onerror=alert(1)>"));

        var turn = await svc.AskAsync(null, "Frage", Actor());

        // the renderer escapes rather than deletes: the markup shows as text and can never execute
        Assert.DoesNotContain("<script", turn.Answer.Html);
        Assert.DoesNotContain("<img", turn.Answer.Html);
        Assert.Contains("&lt;script&gt;", turn.Answer.Html);
    }

    [Fact]
    public async Task Ask_TitlesTheConversationFromTheFirstQuestion()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);

        await svc.AskAsync(null, "Wer führt die Ballas?", Actor());

        var row = Assert.Single(await svc.GetConversationsAsync(Actor()));
        Assert.Equal("Wer führt die Ballas?", row.Title);
    }

    [Fact]
    public async Task Ask_ReplaysTheHistoryIntoTheFollowUp()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var seen = new List<int>();
        var (svc, _) = Build(ctx, call =>
        {
            seen.Add(call.Messages.Count);
            return Answer();
        });

        var first = await svc.AskAsync(null, "Erste Frage", Actor());
        await svc.AskAsync(first.ConversationId, "Und was ist mit deren Fraktion?", Actor());

        // turn 1: system + question. turn 2: system + question + answer + new question
        Assert.Equal(2, seen[0]);
        Assert.Equal(4, seen[1]);
    }

    [Fact]
    public async Task Ask_KeepsToolResults_AndReplaysThemWhileTheScopeIsUnchanged()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var transcript = new List<LlmMessage> { LlmMessage.Tool("c1", "suche_akten", "• Person | Otto Offen") };
        var replayed = new List<string>();
        var (svc, _) = Build(ctx, call =>
        {
            replayed.Add(string.Join("\n", call.Messages.Select(m => m.Content)));
            return Answer(transcript: transcript);
        });

        var first = await svc.AskAsync(null, "Erste Frage", Actor());
        await svc.AskAsync(first.ConversationId, "Zweite Frage", Actor());

        Assert.Contains("Otto Offen", replayed[1]);
    }

    [Fact]
    public async Task Ask_DropsToolResultsFromTheReplay_WhenTheScopeChanged()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var transcript = new List<LlmMessage> { LlmMessage.Tool("c1", "lies_akte", "Geheime Akteninhalte") };
        var replayed = new List<string>();
        var (svc, _) = Build(ctx, call =>
        {
            replayed.Add(string.Join("\n", call.Messages.Select(m => m.Content)));
            return Answer(transcript: transcript);
        });

        // turn 1 as leadership, turn 2 after losing that right
        var first = await svc.AskAsync(null, "Erste Frage", Actor(rank: Rank.Director));
        await svc.AskAsync(first.ConversationId, "Zweite Frage", Actor(rank: Rank.JuniorAgent));

        // the tool text was authorised under rights the agent no longer has
        Assert.DoesNotContain("Geheime Akteninhalte", replayed[1]);
        // the agent's own turn survives; only the tool result is withheld
        Assert.Contains("Erste Frage", replayed[1]);
    }

    [Fact]
    public async Task Conversations_AreOwnerPrivate()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);
        var turn = await svc.AskAsync(null, "Meine Frage", Actor());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetMessagesAsync(turn.ConversationId, Actor(Other)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeleteAsync(turn.ConversationId, Actor(Other)));
        Assert.Empty(await svc.GetConversationsAsync(Actor(Other)));
    }

    [Fact]
    public async Task Conversations_AreReadableByTheAiOwner()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);
        var turn = await svc.AskAsync(null, "Meine Frage", Actor());
        var aiOwner = ClaimsPrincipalBuilder.Agent(Other).WithRank(Rank.Director).AsAiOwner().Build();

        var messages = await svc.GetMessagesAsync(turn.ConversationId, aiOwner);

        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task Rename_AndDelete_WorkForTheOwner()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);
        var turn = await svc.AskAsync(null, "Meine Frage", Actor());

        await svc.RenameAsync(turn.ConversationId, "Ballas-Recherche", Actor());
        Assert.Equal("Ballas-Recherche", (await svc.GetConversationsAsync(Actor())).Single().Title);

        await svc.DeleteAsync(turn.ConversationId, Actor());
        Assert.Empty(await svc.GetConversationsAsync(Actor()));
    }

    [Fact]
    public async Task Delete_RemovesTheThreadAndItsMessages()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);
        var turn = await svc.AskAsync(null, "Meine Frage", Actor());

        await svc.DeleteAsync(turn.ConversationId, Actor());

        await using var db = ctx.NewContext();
        Assert.Empty(await db.NooseiConversations.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetMessagesAsync(turn.ConversationId, Actor()));
    }

    [Fact]
    public async Task Ask_RejectsAnEmptyQuestion()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, gateway) = Build(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AskAsync(null, "   ", Actor()));

        await gateway.DidNotReceive().AskAsync(Arg.Any<NooseiCall>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("partner")]
    [InlineData("demo")]
    [InlineData("supervision")]
    public async Task Ask_DeniesTheRolesWithoutNooseiAccess(string role)
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        var (svc, _) = Build(ctx);
        var builder = ClaimsPrincipalBuilder.Agent(Owner).WithRank(Rank.SpecialAgent);
        var actor = role switch
        {
            "partner" => builder.AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build(),
            "demo" => builder.AsDemo().Build(),
            _ => builder.AsTeamLead().Build(),
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AskAsync(null, "Frage", actor));
    }

    [Fact]
    public async Task Ask_OffersTheToolCatalogue()
    {
        using var ctx = new SqliteTestContext();
        await SeedAgentsAsync(ctx);
        NooseiCall? seen = null;
        var (svc, _) = Build(ctx, call =>
        {
            seen = call;
            return Answer();
        });

        await svc.AskAsync(null, "Frage", Actor());

        Assert.NotNull(seen?.Tools);
        Assert.NotEmpty(seen!.Tools!);
        Assert.NotNull(seen.ToolExecutor);
    }
}
