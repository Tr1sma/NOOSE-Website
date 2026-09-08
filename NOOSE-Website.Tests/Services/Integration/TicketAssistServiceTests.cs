using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Llm;
using NOOSE_Website.Services.Public;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>NOOSEI drafting a ticket answer: the gates, the prompt, and what the draft is scrubbed of.</summary>
public sealed class TicketAssistServiceTests
{
    private const string TicketId = "t1";

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.SpecialAgent).Build();

    private static ClaimsPrincipal Supervision()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static NooseiAnswer Answer(string text)
        => new(text, LlmUsage.Empty, new LlmQuotaCharge(120, 0.001m, LlmQuotaStatus.Empty, null, true), 1, [], false);

    private static TicketAssistService Build(SqliteTestContext ctx, string answer,
        Action<NooseiCall>? inspect = null, bool configured = true)
    {
        var gateway = Substitute.For<INooseiGateway>();
        gateway.IsConfigured.Returns(configured);
        gateway.AskAsync(Arg.Any<NooseiCall>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                inspect?.Invoke(call.Arg<NooseiCall>());
                return Answer(answer);
            });

        var settings = Substitute.For<INooseiSettingsService>();
        settings.GetAddendumAsync(Arg.Any<CancellationToken>()).Returns((string?)null);
        return new TicketAssistService(ctx.Factory, gateway, settings);
    }

    /// <summary>A citizen ticket with one line from each side.</summary>
    private static async Task<SqliteTestContext> SeededAsync(TicketArt kind = TicketArt.Fuehrungsebene,
        bool withMessages = true)
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        db.Tickets.Add(new Ticket
        {
            Id = TicketId,
            CaseNumber = "NOOSE-T-2026-0001",
            Subject = "Beschädigtes Fahrzeug",
            Kind = kind,
            Category = TicketKategorie.Anzeige,
            Status = TicketStatus.Offen,
            LastActivityAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        });
        if (withMessages)
        {
            db.TicketNachrichten.Add(new TicketNachricht
            {
                Id = "m1",
                TicketId = TicketId,
                Audience = TicketMessageAudience.Buerger,
                Text = "Mein Fahrzeug wurde beschädigt, was kann ich tun?",
                AuthorIsCitizen = true,
                CreatedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            });
            db.TicketNachrichten.Add(new TicketNachricht
            {
                Id = "m2",
                TicketId = TicketId,
                Audience = TicketMessageAudience.Intern,
                Text = "Interne Einschätzung: Zuständigkeit prüfen.",
                AuthorAgentId = "lead",
                CreatedAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            });
        }
        await db.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task TheDraftComesBackWithItsCost()
    {
        using var ctx = await SeededAsync();
        var svc = Build(ctx, "Guten Tag, wir haben Ihr Anliegen aufgenommen und melden uns.");

        var result = await svc.SuggestReplyAsync(TicketId, Leader());

        Assert.StartsWith("Guten Tag", result.Text);
        Assert.Equal(120L, result.QuotaTokens);
    }

    [Fact]
    public async Task ThePromptCarriesTheCitizenThreadAndNotTheInternalOne()
    {
        using var ctx = await SeededAsync();
        NooseiCall? seen = null;
        var svc = Build(ctx, "Guten Tag, wir melden uns.", call => seen = call);

        await svc.SuggestReplyAsync(TicketId, Leader());

        var prompt = seen!.LoggedPrompt!;
        Assert.Contains("Mein Fahrzeug wurde beschädigt", prompt);
        // the internal thread is not the agency's answer to anybody and never reaches the model here
        Assert.DoesNotContain("Interne Einschätzung", prompt);
        // the call points at the ticket, so the NOOSEI log can be traced back to it
        Assert.Equal(nameof(Ticket), seen.EntityType);
        Assert.Equal(TicketId, seen.EntityId);
    }

    [Fact]
    public async Task AMentionTokenIsScrubbedOutOfTheDraft()
    {
        // the citizen thread refuses tokens at the service, so a draft carrying one could never be sent
        using var ctx = await SeededAsync();
        var token = MentionParser.Token("Agent", "11111111-1111-1111-1111-111111111111");
        var svc = Build(ctx, $"Guten Tag, {token} kümmert sich darum.");

        var result = await svc.SuggestReplyAsync(TicketId, Leader());

        Assert.DoesNotContain("@{", result.Text);
    }

    [Fact]
    public async Task AnOverlongDraftIsClippedToOneMessage()
    {
        using var ctx = await SeededAsync();
        var svc = Build(ctx, new string('x', TicketRules.MaxMessageLength + 500));

        var result = await svc.SuggestReplyAsync(TicketId, Leader());

        Assert.True(result.Text.Length <= TicketRules.MaxMessageLength);
    }

    [Fact]
    public async Task AnEmptyAnswerIsRefusedRatherThanClearingTheComposer()
    {
        using var ctx = await SeededAsync();
        var svc = Build(ctx, "   ");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SuggestReplyAsync(TicketId, Leader()));
    }

    [Fact]
    public async Task OnlyTheDeskMayDraft()
    {
        using var ctx = await SeededAsync();
        var svc = Build(ctx, "Guten Tag.");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.SuggestReplyAsync(TicketId, Junior()));
        // the read-only supervision fails on the write guard, which runs before the rank check
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.SuggestReplyAsync(TicketId, Supervision()));
    }

    [Fact]
    public async Task AnInternalTicketHasNoCitizenAnswerToDraft()
    {
        using var ctx = await SeededAsync(TicketArt.Intern);
        var svc = Build(ctx, "Guten Tag.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SuggestReplyAsync(TicketId, Leader()));
    }

    [Fact]
    public async Task AnEmptyThreadAndAnUnconfiguredModelBothRefuse()
    {
        using var empty = await SeededAsync(withMessages: false);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build(empty, "Guten Tag.").SuggestReplyAsync(TicketId, Leader()));

        using var ctx = await SeededAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build(ctx, "Guten Tag.", configured: false).SuggestReplyAsync(TicketId, Leader()));
    }
}
