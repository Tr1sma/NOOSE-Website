using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Timeline;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="GlobalChronikService"/> against in-memory SQLite.</summary>
public sealed class GlobalChronikServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
    private static ClaimsPrincipal Junior() => ClaimsPrincipalBuilder.Agent("low").WithRank(Rank.JuniorAgent).Build();
    private static ClaimsPrincipal Partner() =>
        ClaimsPrincipalBuilder.Agent("p").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

    private static ChronikQuery Window(string? type = null, string? agent = null)
        => new(Now.AddDays(-30), Now.AddDays(1), type, agent);

    private static void Audit(NOOSE_Website.Data.AppDbContext db, string type, string id, AuditAction action, string agent = "a1")
        => db.AuditLogs.Add(new AuditLog { EntityType = type, EntityId = id, Action = action, Timestamp = Now, AgentId = agent, AgentName = agent });

    [Fact]
    public async Task GetEventsAsync_ReturnsLifecycleAndClassification_ForLeadership()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Max"));
            Audit(db, nameof(Person), "p1", AuditAction.Created);
            db.ClassificationHistory.Add(new ClassificationHistory
            {
                EntityType = nameof(Person), EntityId = "p1", Value = Classification.SuspicionCase,
                Timestamp = Now, AgentId = "a1", AgentName = "a1",
            });
            db.SaveChanges();
        }
        var svc = new GlobalChronikService(ctx.Factory);

        var result = await svc.GetEventsAsync(Window(), Leader());

        Assert.Equal(2, result.Events.Count);
        Assert.All(result.Events, e => Assert.Equal("Max", e.Name));
        Assert.Contains(result.Events, e => e.Category == TimelineCategory.Classification);
    }

    [Fact]
    public async Task GetEventsAsync_HidesClassifiedRecord_FromNonLeadership()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "s1", name: "Geheim", configure: p => p.IsClassified = true));
            Audit(db, nameof(Person), "s1", AuditAction.Created);
            db.SaveChanges();
        }
        var svc = new GlobalChronikService(ctx.Factory);

        Assert.Single((await svc.GetEventsAsync(Window(), Leader())).Events);
        Assert.Empty((await svc.GetEventsAsync(Window(), Junior())).Events);
    }

    [Fact]
    public async Task GetEventsAsync_ShowsDeletionEvent_ForSoftDeletedRecord()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "d1", name: "Weg", configure: p =>
            {
                p.IsDeleted = true;
                p.DeletedAt = Now;
            }));
            Audit(db, nameof(Person), "d1", AuditAction.Deleted);
            db.SaveChanges();
        }
        var svc = new GlobalChronikService(ctx.Factory);

        var events = (await svc.GetEventsAsync(Window(), Leader())).Events;
        var e = Assert.Single(events);
        Assert.Equal("Weg", e.Name);
        Assert.Equal(TimelineCategory.Deletion, e.Category);
    }

    [Fact]
    public async Task GetEventsAsync_Empty_ForPartner()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Max"));
            Audit(db, nameof(Person), "p1", AuditAction.Created);
            db.SaveChanges();
        }
        var svc = new GlobalChronikService(ctx.Factory);

        Assert.Empty((await svc.GetEventsAsync(Window(), Partner())).Events);
    }

    [Fact]
    public async Task GetEventsAsync_FiltersByType()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Max"));
            db.Factions.Add(Seed.Faction(id: "f1", name: "Ballas"));
            Audit(db, nameof(Person), "p1", AuditAction.Created);
            Audit(db, nameof(NOOSE_Website.Data.Entities.Factions.Faction), "f1", AuditAction.Created);
            db.SaveChanges();
        }
        var svc = new GlobalChronikService(ctx.Factory);

        var events = (await svc.GetEventsAsync(Window(type: nameof(NOOSE_Website.Data.Entities.Factions.Faction)), Leader())).Events;
        var e = Assert.Single(events);
        Assert.Equal("Ballas", e.Name);
    }

    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsActingAgents()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p1", name: "Max"));
            Audit(db, nameof(Person), "p1", AuditAction.Created, agent: "a1");
            db.SaveChanges();
        }
        var svc = new GlobalChronikService(ctx.Factory);

        var options = await svc.GetFilterOptionsAsync(Leader());

        Assert.Contains("Person", options.Types);
        Assert.Contains(options.Agents, a => a.Id == "a1");
    }
}
