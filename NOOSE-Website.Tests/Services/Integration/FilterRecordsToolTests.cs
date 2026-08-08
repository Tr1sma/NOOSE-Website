using System.Security.Claims;
using System.Text.Json;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Llm.Tools;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The counting tool. A count is an assertion about the stock, so the interesting cases are the ones
/// where a number could say more than the rows are allowed to.</summary>
public sealed class FilterRecordsToolTests
{
    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.JuniorAgent).Build();

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>Real list services, not stubs: the visibility this tool relies on lives in them.</summary>
    private static FilterRecordsTool Tool(SqliteTestContext ctx)
    {
        var people = new PersonService(ctx.Factory, Substitute.For<IFileStorageService>(),
            Substitute.For<IProfileSuggestionService>(), Substitute.For<ICaseNumberService>(),
            Substitute.For<IThreatScoreService>(), Substitute.For<INotificationService>());
        var factions = new FactionService(ctx.Factory, Substitute.For<ICaseNumberService>(),
            Substitute.For<IProfileSuggestionService>(), people, Substitute.For<IFactionPhotoStorageService>(),
            Substitute.For<IThreatScoreService>(), Substitute.For<INotificationService>());
        return new FilterRecordsTool(people, factions,
            Substitute.For<IPersonGroupService>(),
            Substitute.For<IPartyService>(),
            Substitute.For<ICaseService>(),
            Substitute.For<IOperationService>());
    }

    private static void Seed(SqliteTestContext ctx)
    {
        using var db = ctx.NewContext();
        db.People.Add(Infrastructure.Seed.Person(id: "p-open", name: "Otto Offen", configure: p =>
        {
            p.Classification = Classification.SuspicionCase;
            p.ThreatScore = 40;
        }));
        db.People.Add(Infrastructure.Seed.Person(id: "p-plain", name: "Paula Prüf", configure: p =>
        {
            p.Classification = Classification.ReviewCase;
            p.ThreatScore = 80;
        }));
        db.People.Add(Infrastructure.Seed.Person(id: "p-secret", name: "Gerd Geheim", configure: p =>
        {
            p.Classification = Classification.SuspicionCase;
            p.IsClassified = true;
            p.ThreatScore = 90;
        }));
        db.SaveChanges();
    }

    [Fact]
    public async Task Count_LeavesOutWhatTheAskerMayNotSee()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        var tool = Tool(ctx);
        var args = Args("""{"typ":"Person","einstufung":"Verdachtsfall","nur_anzahl":true}""");

        var junior = await tool.InvokeAsync(args, NooseiToolContext.From(Junior()));
        var leader = await tool.InvokeAsync(args, NooseiToolContext.From(Leader()));

        // the classified suspicion case must not show up as a +1 either — a count is an answer too
        Assert.StartsWith("1 Person entspricht", junior.Text);
        Assert.StartsWith("2 Personen", leader.Text);
        Assert.DoesNotContain("Gerd Geheim", junior.Text);
    }

    [Fact]
    public async Task CountOnly_MatchesTheNumberOfListedRecords()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        var tool = Tool(ctx);

        var listed = await tool.InvokeAsync(Args("""{"typ":"Person"}"""), NooseiToolContext.From(Leader()));
        var counted = await tool.InvokeAsync(Args("""{"typ":"Person","nur_anzahl":true}"""), NooseiToolContext.From(Leader()));

        Assert.Equal(3, listed.Text.Split('•').Length - 1);
        Assert.StartsWith("3 Personen", counted.Text);
        Assert.DoesNotContain("•", counted.Text);
    }

    [Fact]
    public async Task ScoreRange_Filters()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);

        var result = await Tool(ctx).InvokeAsync(
            Args("""{"typ":"Person","score_min":70}"""), NooseiToolContext.From(Leader()));

        Assert.Contains("Paula Prüf", result.Text);
        Assert.Contains("Gerd Geheim", result.Text);
        Assert.DoesNotContain("Otto Offen", result.Text);
    }

    [Fact]
    public async Task Rows_AreLabelledInGerman()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);

        var result = await Tool(ctx).InvokeAsync(
            Args("""{"typ":"Person","einstufung":"Prüffall"}"""), NooseiToolContext.From(Leader()));

        // the model copies names verbatim, so a raw enum identifier would end up in the answer
        Assert.Contains("Prüffall", result.Text);
        Assert.Contains("Lebensstatus: Lebend", result.Text);
        Assert.DoesNotContain("ReviewCase", result.Text);
        Assert.DoesNotContain("Alive", result.Text);
    }

    [Fact]
    public async Task Matches_CarryRefs_SoTheAnswerCanCiteThem()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);

        var result = await Tool(ctx).InvokeAsync(
            Args("""{"typ":"Person","score_min":80}"""), NooseiToolContext.From(Leader()));

        Assert.NotNull(result.Refs);
        Assert.Equal(2, result.Refs!.Count);
        Assert.All(result.Refs, r => Assert.Equal(nameof(NOOSE_Website.Data.Entities.People.Person), r.Kind));
    }

    [Fact]
    public async Task ChangedWithin_FindsARecordThatWasNeverEditedSinceItWasCreated()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            // the audit interceptor stamps ModifiedAt only on an update, so a fresh record has none at all
            db.People.Add(Infrastructure.Seed.Person(id: "p-neu", name: "Neu Angelegt", configure: p =>
            {
                p.CreatedAt = DateTime.UtcNow.AddDays(-2);
                p.ModifiedAt = null;
            }));
            db.People.Add(Infrastructure.Seed.Person(id: "p-alt", name: "Alt Vergessen", configure: p =>
            {
                p.CreatedAt = DateTime.UtcNow.AddDays(-400);
                p.ModifiedAt = DateTime.UtcNow.AddDays(-300);
            }));
            await db.SaveChangesAsync();
        }

        var result = await Tool(ctx).InvokeAsync(
            Args("""{"typ":"Person","geaendert_seit_tagen":7}"""), NooseiToolContext.From(Leader()));

        Assert.Contains("Neu Angelegt", result.Text);
        Assert.DoesNotContain("Alt Vergessen", result.Text);
    }

    [Fact]
    public async Task SingleMatch_IsCountedInGrammaticalGerman()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);

        var result = await Tool(ctx).InvokeAsync(
            Args("""{"typ":"Person","einstufung":"Prüffall","nur_anzahl":true}"""), NooseiToolContext.From(Leader()));

        Assert.StartsWith("1 Person entspricht", result.Text);
    }

    [Fact]
    public async Task UnknownType_IsRefusedWithoutTouchingAService()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);

        var result = await Tool(ctx).InvokeAsync(
            Args("""{"typ":"Hausmeister"}"""), NooseiToolContext.From(Leader()));

        Assert.True(result.IsError);
    }
}
