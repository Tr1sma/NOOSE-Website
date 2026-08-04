using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Graph;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Tests for the analysis workbench: GraphAnalytics (pure), opt-in graph analytics, and saved layouts.</summary>
public sealed class GraphWorkbenchTests
{
    private static ClaimsPrincipal Agent(string id) => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SpecialAgent).Build();
    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    // ==================== GraphAnalytics (pure) ====================

    [Fact]
    public void Betweenness_Star_CenterIsKeyFigure()
    {
        var nodes = new[] { "c", "a", "b", "d", "e" };
        var edges = new (string, string)[] { ("c", "a"), ("c", "b"), ("c", "d"), ("c", "e") };

        var bc = GraphAnalytics.Betweenness(nodes, edges);

        Assert.Equal(1.0, bc["c"], 3);
        Assert.Equal(0.0, bc["a"], 3);
        Assert.True(GraphAnalytics.IsKey(bc["c"]));
        Assert.False(GraphAnalytics.IsKey(bc["a"]));
    }

    [Fact]
    public void Communities_TwoDisjointCliques_YieldTwoCommunities()
    {
        var nodes = new[] { "a1", "a2", "a3", "b1", "b2", "b3" };
        var edges = new (string, string)[]
        {
            ("a1", "a2"), ("a2", "a3"), ("a3", "a1"),
            ("b1", "b2"), ("b2", "b3"), ("b3", "b1"),
        };

        var comm = GraphAnalytics.Communities(nodes, edges);

        Assert.Equal(2, comm.Values.Distinct().Count());
        Assert.Equal(comm["a1"], comm["a3"]);
        Assert.Equal(comm["b1"], comm["b3"]);
        Assert.NotEqual(comm["a1"], comm["b1"]);
    }

    [Fact]
    public void Betweenness_TinyGraph_AllZero()
    {
        var bc = GraphAnalytics.Betweenness(new[] { "a", "b" }, new (string, string)[] { ("a", "b") });
        Assert.All(bc.Values, v => Assert.Equal(0.0, v, 6));
    }

    // ==================== GraphService opt-in analytics ====================

    [Fact]
    public async Task GetGraphAsync_MarksKeyFigure_WhenCentralityRequested()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "a", name: "A"));
            db.People.Add(Seed.Person(id: "b", name: "B"));
            db.People.Add(Seed.Person(id: "c", name: "C"));
            db.PersonRelations.Add(new PersonRelation { PersonAId = "a", PersonBId = "b", Type = RelationType.Known });
            db.PersonRelations.Add(new PersonRelation { PersonAId = "b", PersonBId = "c", Type = RelationType.Known });
            db.SaveChanges();
        }
        var svc = new GraphService(ctx.Factory);

        var data = await svc.GetGraphAsync(new GraphQuery(ComputeCentrality: true, ComputeCommunities: true), Leader());

        Assert.Equal(3, data.Node.Count);
        Assert.True(data.Node.Single(n => n.Id == $"{nameof(Person)}:b").IsKeyFigure);
        Assert.False(data.Node.Single(n => n.Id == $"{nameof(Person)}:a").IsKeyFigure);
        // a–b–c is one connected component
        Assert.Single(data.Node.Select(n => n.CommunityId).Distinct());
    }

    [Fact]
    public async Task GetGraphAsync_NoAnalytics_ByDefault()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "a", name: "A"));
            db.People.Add(Seed.Person(id: "b", name: "B"));
            db.PersonRelations.Add(new PersonRelation { PersonAId = "a", PersonBId = "b", Type = RelationType.Known });
            db.SaveChanges();
        }
        var svc = new GraphService(ctx.Factory);

        var data = await svc.GetGraphAsync(new GraphQuery(), Leader());

        Assert.All(data.Node, n => Assert.False(n.IsKeyFigure));
    }

    // ==================== GraphCanvasLayoutService ====================

    [Fact]
    public async Task SaveAsync_InsertsForOwner_AndUpsertsSameName()
    {
        using var ctx = new SqliteTestContext();
        var svc = new GraphCanvasLayoutService(ctx.Factory);
        var me = Agent("me");

        await svc.SaveAsync("Ansicht 1", "{\"a\":1}", me);
        await svc.SaveAsync("Ansicht 1", "{\"a\":2}", me);

        var list = await svc.GetForAgentAsync("me");
        var single = Assert.Single(list);
        Assert.Equal("{\"a\":2}", single.LayoutJson);
    }

    [Fact]
    public async Task SaveAsync_Throws_ForOnlyReader()
    {
        using var ctx = new SqliteTestContext();
        var svc = new GraphCanvasLayoutService(ctx.Factory);
        var onlyReader = ClaimsPrincipalBuilder.Agent("r").AsTeamLead().Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.SaveAsync("X", "{}", onlyReader));
    }

    [Fact]
    public async Task DeleteAsync_OnlyOwnLayout()
    {
        using var ctx = new SqliteTestContext();
        var svc = new GraphCanvasLayoutService(ctx.Factory);
        var saved = await svc.SaveAsync("V", "{}", Agent("owner"));

        await svc.DeleteAsync(saved.Id, Agent("intruder"));
        Assert.Single(await svc.GetForAgentAsync("owner"));

        await svc.DeleteAsync(saved.Id, Agent("owner"));
        Assert.Empty(await svc.GetForAgentAsync("owner"));
    }

    // ==================== GraphViewPayload (positions + workbench state) ====================

    private const string Positions = "{\"Person:1\":{\"x\":10,\"y\":-20}}";

    [Fact]
    public void Wrap_Unwrap_RoundTripsPositionsAndView()
    {
        var view = new GraphViewState(
            Centrality: true, Community: true, FocusMode: true,
            FocusType: "Faction", FocusId: "f1", FocusName: "Ballas",
            Depth: 3, Types: new[] { "Person", "Faction" }, Kind: "Konflikt");

        var (positions, back) = GraphViewPayload.Unwrap(GraphViewPayload.Wrap(Positions, view));

        Assert.NotNull(back);
        Assert.True(back!.Centrality);
        Assert.True(back.Community);
        Assert.True(back.FocusMode);
        Assert.Equal("Faction", back.FocusType);
        Assert.Equal("f1", back.FocusId);
        Assert.Equal("Ballas", back.FocusName);
        Assert.Equal(3, back.Depth);
        Assert.Equal(new[] { "Person", "Faction" }, back.Types);
        Assert.Equal("Konflikt", back.Kind);
        Assert.Contains("\"Person:1\"", positions);
        Assert.Contains("\"x\":10", positions.Replace(" ", string.Empty));
    }

    [Fact]
    public void Unwrap_LegacyFlatPositionMap_KeepsPositionsAndHasNoView()
    {
        var (positions, view) = GraphViewPayload.Unwrap(Positions);

        Assert.Null(view);
        Assert.Equal(Positions, positions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nicht mal JSON")]
    [InlineData("[1,2,3]")]
    public void Unwrap_Garbage_YieldsEmptyPositionsAndNoView(string? stored)
    {
        var (positions, view) = GraphViewPayload.Unwrap(stored);

        Assert.Null(view);
        Assert.Equal("{}", positions);
    }

    [Fact]
    public void Wrap_BrokenPositions_StillStoresTheView()
    {
        var (positions, view) = GraphViewPayload.Unwrap(
            GraphViewPayload.Wrap("kaputt", new GraphViewState(Centrality: true)));

        Assert.Equal("{}", positions);
        Assert.True(view?.Centrality);
    }

    [Fact]
    public void Unwrap_ClampsDepthAndUnknownKind()
    {
        var stored = GraphViewPayload.Wrap(Positions, new GraphViewState(Depth: 9, Kind: "Quatsch"));

        var (_, view) = GraphViewPayload.Unwrap(stored);

        Assert.Equal(3, view!.Depth);
        Assert.Equal("alle", view.Kind);
    }

    [Fact]
    public void Unwrap_ViewWithoutTypes_YieldsNullTypes()
    {
        var stored = GraphViewPayload.Wrap(Positions, new GraphViewState(Community: true));

        var (_, view) = GraphViewPayload.Unwrap(stored);

        Assert.Null(view!.Types);
        Assert.True(view.Community);
        Assert.False(view.Centrality);
    }
}
