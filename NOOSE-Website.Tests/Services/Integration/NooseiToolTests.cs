using System.Security.Claims;
using System.Text.Json;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Llm.Tools;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Every NOOSEI tool answers within the asking agent's own scope. These tests are about what a tool must NOT return.</summary>
public sealed class NooseiToolTests
{
    private const string OpenPerson = "p-open";
    private const string SecretPerson = "p-secret";
    private const string FactionId = "f1";

    private static ClaimsPrincipal Junior()
        => ClaimsPrincipalBuilder.Agent("junior").WithRank(Rank.JuniorAgent).Build();

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static void Seed(SqliteTestContext ctx)
    {
        using var db = ctx.NewContext();
        db.People.Add(NOOSE_Website.Tests.Infrastructure.Seed.Person(id: OpenPerson, name: "Otto Offen"));
        db.People.Add(NOOSE_Website.Tests.Infrastructure.Seed.Person(id: SecretPerson, name: "Gerd Geheim",
            configure: p => p.IsClassified = true));
        db.Factions.Add(NOOSE_Website.Tests.Infrastructure.Seed.Faction(id: FactionId, name: "Ballas"));
        db.SaveChanges();
    }

    // ---- lies_akte ----

    [Fact]
    public async Task ReadRecord_GivesTheSameAnswer_ForMissingAndForbidden()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        var tool = new ReadRecordTool(ctx.Factory, Substitute.For<IAccessLogService>());

        var forbidden = await tool.InvokeAsync(
            Args($$"""{"typ":"Person","id":"{{SecretPerson}}"}"""), NooseiToolContext.From(Junior()));
        var missing = await tool.InvokeAsync(
            Args("""{"typ":"Person","id":"gibt-es-nicht"}"""), NooseiToolContext.From(Junior()));

        // identical wording on purpose: anything else turns the tool into an existence oracle
        Assert.Equal(missing.Text, forbidden.Text);
        Assert.True(forbidden.IsError);
        Assert.DoesNotContain("Gerd Geheim", forbidden.Text);
    }

    [Fact]
    public async Task ReadRecord_ReturnsTheDossier_ForAVisibleRecord()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        var accessLog = Substitute.For<IAccessLogService>();
        var tool = new ReadRecordTool(ctx.Factory, accessLog);

        var result = await tool.InvokeAsync(
            Args($$"""{"typ":"Person","id":"{{OpenPerson}}"}"""), NooseiToolContext.From(Junior()));

        Assert.False(result.IsError);
        Assert.Contains("Otto Offen", result.Text);
        // a read through NOOSEI is still a read
        await accessLog.Received(1).LogViewAsync("Person", OpenPerson, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadRecord_LetsLeadershipSeeAClassifiedRecord()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        var tool = new ReadRecordTool(ctx.Factory, Substitute.For<IAccessLogService>());

        var result = await tool.InvokeAsync(
            Args($$"""{"typ":"Person","id":"{{SecretPerson}}"}"""), NooseiToolContext.From(Leader()));

        Assert.Contains("Gerd Geheim", result.Text);
    }

    [Fact]
    public async Task ReadRecord_RespectsTheCompactBudget()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(NOOSE_Website.Tests.Infrastructure.Seed.Person(id: "p-long", name: "Lang",
                configure: p => p.Description = new string('x', 20_000)));
            db.SaveChanges();
        }
        var tool = new ReadRecordTool(ctx.Factory, Substitute.For<IAccessLogService>());

        var compact = await tool.InvokeAsync(
            Args("""{"typ":"Person","id":"p-long","umfang":"kompakt"}"""), NooseiToolContext.From(Leader()));
        var full = await tool.InvokeAsync(
            Args("""{"typ":"Person","id":"p-long","umfang":"voll"}"""), NooseiToolContext.From(Leader()));

        Assert.True(compact.Text.Length < full.Text.Length);
        Assert.True(full.Text.Length <= NooseiLimits.MaxToolResultChars + 32);
    }

    // ---- zeige_verbindungen ----

    [Fact]
    public async Task ListRelated_MasksAClassifiedLink_ForANonLeader()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        using (var db = ctx.NewContext())
        {
            db.Links.Add(new Link
            {
                SourceType = nameof(Faction), SourceId = FactionId,
                TargetType = nameof(Person), TargetId = SecretPerson, Label = "Mitglied",
            });
            db.SaveChanges();
        }
        var tool = new ListRelatedTool(ctx.Factory);

        var junior = await tool.InvokeAsync(
            Args($$"""{"typ":"Fraktion","id":"{{FactionId}}"}"""), NooseiToolContext.From(Junior()));
        var leader = await tool.InvokeAsync(
            Args($$"""{"typ":"Fraktion","id":"{{FactionId}}"}"""), NooseiToolContext.From(Leader()));

        Assert.DoesNotContain("Gerd Geheim", junior.Text);
        Assert.Contains("(Verschlusssache)", junior.Text);
        Assert.Contains("Gerd Geheim", leader.Text);
    }

    [Fact]
    public async Task ListRelated_HidesALinkedTaskforce_ForANonMember()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        using (var db = ctx.NewContext())
        {
            db.Taskforces.Add(new Taskforce
            {
                Id = "tf1", Name = "Operation Nachtfalke", CaseNumber = "NOOSE-TF-2026-0001",
                Scope = TaskforceScope.InternalAgency, Status = TaskforceStatus.Approved,
            });
            db.Links.Add(new Link
            {
                SourceType = nameof(Faction), SourceId = FactionId,
                TargetType = nameof(Taskforce), TargetId = "tf1",
            });
            db.SaveChanges();
        }
        var tool = new ListRelatedTool(ctx.Factory);

        var outsider = await tool.InvokeAsync(
            Args($$"""{"typ":"Fraktion","id":"{{FactionId}}"}"""), NooseiToolContext.From(Junior()));

        Assert.DoesNotContain("Operation Nachtfalke", outsider.Text);
        Assert.DoesNotContain("NOOSE-TF-2026-0001", outsider.Text);
    }

    [Fact]
    public async Task ListRelated_RefusesAnInvisibleRoot()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx);
        var tool = new ListRelatedTool(ctx.Factory);

        var result = await tool.InvokeAsync(
            Args($$"""{"typ":"Person","id":"{{SecretPerson}}"}"""), NooseiToolContext.From(Junior()));

        Assert.True(result.IsError);
    }

    // ---- suche_akten ----

    [Fact]
    public async Task SearchRecords_PassesTheAskingAgentsScopeThrough()
    {
        using var ctx = new SqliteTestContext();
        var search = Substitute.For<ISearchService>();
        ViewerScope? seen = null;
        search.SearchAsync(Arg.Any<NOOSE_Website.Models.Common.SearchCriteria>(), Arg.Any<ViewerScope>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                seen = call.ArgAt<ViewerScope>(1);
                return Task.FromResult(new List<NOOSE_Website.Models.Common.SearchResultGroup>());
            });
        var tool = new SearchRecordsTool(search);

        await tool.InvokeAsync(Args("""{"suchtext":"Ballas"}"""), NooseiToolContext.From(Junior()));

        Assert.NotNull(seen);
        Assert.False(seen!.Value.MayClassifiedRead);
    }

    [Fact]
    public async Task SearchRecords_MapsGermanTypeNamesToTheSearchCategories()
    {
        using var ctx = new SqliteTestContext();
        var search = Substitute.For<ISearchService>();
        NOOSE_Website.Models.Common.SearchCriteria? criteria = null;
        search.SearchAsync(Arg.Any<NOOSE_Website.Models.Common.SearchCriteria>(), Arg.Any<ViewerScope>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                criteria = call.ArgAt<NOOSE_Website.Models.Common.SearchCriteria>(0);
                return Task.FromResult(new List<NOOSE_Website.Models.Common.SearchResultGroup>());
            });
        var tool = new SearchRecordsTool(search);

        await tool.InvokeAsync(Args("""{"suchtext":"x","typen":["Fraktion","Vorgang"]}"""), NooseiToolContext.From(Leader()));

        Assert.Equal(["Faction", "Case"], criteria!.Categories);
    }

    [Fact]
    public async Task SearchRecords_RejectsAnEmptyQuery()
    {
        var tool = new SearchRecordsTool(Substitute.For<ISearchService>());

        var result = await tool.InvokeAsync(Args("""{"suchtext":"   "}"""), NooseiToolContext.From(Leader()));

        Assert.True(result.IsError);
    }

    // ---- registry ----

    [Fact]
    public void Registry_ExposesEveryToolOnceAndInAStableOrder()
    {
        var search = new SearchRecordsTool(Substitute.For<ISearchService>());
        var mention = new ResolveMentionTool(Substitute.For<IMentionService>());
        var registry = new NooseiToolRegistry([mention, search]);

        Assert.Equal(["loese_erwaehnung_auf", "suche_akten"], registry.Definitions.Select(d => d.Name).ToArray());
        Assert.NotNull(registry.Find("suche_akten"));
        Assert.Null(registry.Find("gibt_es_nicht"));
    }
}
