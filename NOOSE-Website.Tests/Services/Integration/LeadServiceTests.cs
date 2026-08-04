using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Leads;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="LeadService"/> (signals, dismissal, VS).</summary>
public sealed class LeadServiceTests
{
    private static ClaimsPrincipal Agent(string id = "me") => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SpecialAgent).Build();
    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
    private static ClaimsPrincipal Junior() => ClaimsPrincipalBuilder.Agent("low").WithRank(Rank.JuniorAgent).Build();

    private static LeadService Svc(SqliteTestContext ctx) => new(ctx.Factory, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<List<Lead>> FlatAsync(LeadService svc, ClaimsPrincipal viewer)
        => (await svc.GetFeedAsync(viewer)).SelectMany(g => g.Leads).ToList();

    private static PersonRelation Rel(string a, string b) => new() { PersonAId = a, PersonBId = b, Type = RelationType.Known };

    [Fact]
    public async Task GetFeed_PredictsLink_ForTwoPeopleWithSharedNeighbours()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            foreach (var (id, name) in new[] { ("a", "A"), ("b", "B"), ("c", "C"), ("d", "D") })
            {
                db.People.Add(Seed.Person(id: id, name: name));
            }
            // a & b both connect to c and d, but not to each other
            db.PersonRelations.AddRange(Rel("a", "c"), Rel("a", "d"), Rel("b", "c"), Rel("b", "d"));
            db.SaveChanges();
        }
        var leads = await FlatAsync(Svc(ctx), Leader());

        Assert.Contains(leads, l => l.Kind == LeadKind.LinkPrediction
            && ((l.PrimaryName == "A" && l.SecondaryName == "B") || (l.PrimaryName == "B" && l.SecondaryName == "A")));
    }

    [Fact]
    public async Task GetFeed_FlagsRecentConflict()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Factions.Add(Seed.Faction(id: "f1", name: "Ballas"));
            db.Factions.Add(Seed.Faction(id: "f2", name: "Vagos"));
            db.Links.Add(new Link
            {
                SourceType = nameof(NOOSE_Website.Data.Entities.Factions.Faction), SourceId = "f1",
                TargetType = nameof(NOOSE_Website.Data.Entities.Factions.Faction), TargetId = "f2",
                Kind = LinkKind.Conflict, Automatic = false, CreatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
        var leads = await FlatAsync(Svc(ctx), Leader());

        Assert.Contains(leads, l => l.Kind == LeadKind.NewConflict);
    }

    [Fact]
    public async Task GetFeed_FlagsStaleHighClassification()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Alt", configure: p =>
            {
                p.Classification = Classification.SuspicionCase;
                p.ModifiedAt = DateTime.UtcNow.AddDays(-60);
            }));
            db.SaveChanges();
        }
        var leads = await FlatAsync(Svc(ctx), Leader());

        Assert.Contains(leads, l => l.Kind == LeadKind.StaleHighClassification && l.PrimaryId == "p1");
    }

    [Fact]
    public async Task Ignore_RemovesLead_AndUndoRestores()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Alt", configure: p =>
            {
                p.Classification = Classification.SuspicionCase;
                p.ModifiedAt = DateTime.UtcNow.AddDays(-60);
            }));
            db.SaveChanges();
        }
        var svc = Svc(ctx);
        var lead = Assert.Single(await FlatAsync(svc, Leader()));

        await svc.IgnoreAsync(lead.Key, lead.Kind, Leader());
        Assert.Empty(await FlatAsync(svc, Leader()));

        await svc.UndoIgnoreAsync(lead.Key, Leader());
        Assert.Single(await FlatAsync(svc, Leader()));
    }

    [Fact]
    public async Task Ignore_Throws_ForOnlyReader()
    {
        using var ctx = new SqliteTestContext();
        var svc = Svc(ctx);
        var onlyReader = ClaimsPrincipalBuilder.Agent("r").AsTeamLead().Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.IgnoreAsync("ST|Person|x", LeadKind.StaleHighClassification, onlyReader));
    }

    [Fact]
    public async Task GetFeed_HidesClassifiedLead_FromNonLeadership()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Geheim", configure: p =>
            {
                p.Classification = Classification.SecuredStateThreatening;
                p.IsClassified = true;
                p.ModifiedAt = DateTime.UtcNow.AddDays(-60);
            }));
            db.SaveChanges();
        }
        var svc = Svc(ctx);

        Assert.NotEmpty(await FlatAsync(svc, Leader()));
        Assert.Empty(await FlatAsync(svc, Junior()));
    }
}
