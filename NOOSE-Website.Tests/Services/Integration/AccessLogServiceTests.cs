using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="AccessLogService"/> against in-memory SQLite.</summary>
public sealed class AccessLogServiceTests : IDisposable
{
    private readonly SqliteTestContext _ctx = new();

    private AccessLogService NewService(ICurrentUserService currentUser)
        => new(_ctx.Factory, currentUser);

    private static ICurrentUserService User(CurrentUserInfo info)
    {
        var svc = Substitute.For<ICurrentUserService>();
        svc.GetAsync().Returns(info);
        return svc;
    }

    // ==================== LogViewAsync ====================

    [Fact]
    public async Task LogViewAsync_PersistsRow_WithCurrentUserFields()
    {
        var before = DateTime.UtcNow;
        var svc = NewService(User(new CurrentUserInfo("agent-1", "Falke", false, false, false)));

        await svc.LogViewAsync(nameof(NOOSE_Website.Data.Entities.People.Person), "p-42");

        using var check = _ctx.NewContext();
        var log = Assert.Single(await check.AccessLogs.ToListAsync());
        Assert.Equal("agent-1", log.AgentId);
        Assert.Equal("Falke", log.AgentName);
        Assert.Equal(nameof(NOOSE_Website.Data.Entities.People.Person), log.EntityType);
        Assert.Equal("p-42", log.EntityId);
        Assert.True(log.Timestamp >= before && log.Timestamp <= DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task LogViewAsync_UsesSystemInfo_WhenNoCurrentUser()
    {
        // CurrentUserInfo.System => null Id, "System" name (background/anon path).
        var svc = NewService(User(CurrentUserInfo.System));

        await svc.LogViewAsync("Faction", "f-7");

        using var check = _ctx.NewContext();
        var log = Assert.Single(await check.AccessLogs.ToListAsync());
        Assert.Null(log.AgentId);
        Assert.Equal("System", log.AgentName);
        Assert.Equal("Faction", log.EntityType);
        Assert.Equal("f-7", log.EntityId);
    }

    [Fact]
    public async Task LogViewAsync_AppendsOneRowPerCall()
    {
        var svc = NewService(User(new CurrentUserInfo("agent-1", "Falke", false, false, false)));

        await svc.LogViewAsync("Person", "p-1");
        await svc.LogViewAsync("Person", "p-2");
        await svc.LogViewAsync("Faction", "f-1");

        using var check = _ctx.NewContext();
        var logs = await check.AccessLogs.OrderBy(l => l.Id).ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.Equal(new[] { "p-1", "p-2", "f-1" }, logs.Select(l => l.EntityId).ToArray());
    }

    // ==================== PreviousVisitAsync ====================

    private static readonly CurrentUserInfo Falke = new("agent-1", "Falke", false, false, false);

    private void SeedViews(params (string AgentId, string Type, string Id, DateTime At)[] views)
    {
        using var db = _ctx.NewContext();
        foreach (var v in views)
        {
            db.AccessLogs.Add(new AccessLog
            {
                AgentId = v.AgentId, AgentName = v.AgentId, EntityType = v.Type, EntityId = v.Id, Timestamp = v.At,
            });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task PreviousVisitAsync_CountsOnlyTheAgentsOwnViewsOfThatRecord()
    {
        var now = DateTime.UtcNow;
        SeedViews(
            ("agent-1", "Person", "p-1", now.AddHours(-3)),
            // another agent, another record, and the same id under another type: none of them is this visit
            ("agent-2", "Person", "p-1", now.AddHours(-1)),
            ("agent-1", "Person", "p-2", now.AddHours(-1)),
            ("agent-1", "Faction", "p-1", now.AddHours(-1)));
        var svc = NewService(User(Falke));

        var visit = await svc.PreviousVisitAsync("Person", "p-1");

        Assert.Equal(now.AddHours(-3), visit);
    }

    [Fact]
    public async Task PreviousVisitAsync_FirstVisit_ReturnsNull()
    {
        var svc = NewService(User(Falke));

        Assert.Null(await svc.PreviousVisitAsync("Person", "p-1"));

        // the view of this very visit is not a previous one
        await svc.LogViewAsync("Person", "p-1");
        Assert.Null(await svc.PreviousVisitAsync("Person", "p-1"));
    }

    [Fact]
    public async Task PreviousVisitAsync_IsTheSameBeforeAndAfterThisViewIsLogged()
    {
        var now = DateTime.UtcNow;
        SeedViews(("agent-1", "Person", "p-1", now.AddHours(-2)));
        var svc = NewService(User(Falke));

        var before = await svc.PreviousVisitAsync("Person", "p-1");
        await svc.LogViewAsync("Person", "p-1");
        await svc.LogViewAsync("Person", "p-1");
        var after = await svc.PreviousVisitAsync("Person", "p-1");

        Assert.Equal(now.AddHours(-2), before);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task PreviousVisitAsync_SharedDemoAccount_ReturnsNull()
    {
        SeedViews(("demo-agent", "Person", "p-1", DateTime.UtcNow.AddHours(-2)));
        var svc = NewService(User(new CurrentUserInfo("demo-agent", "Demo", false, false, IsDemo: true)));

        Assert.Null(await svc.PreviousVisitAsync("Person", "p-1"));
    }

    [Fact]
    public async Task PreviousVisitAsync_WithoutAgent_ReturnsNull()
    {
        SeedViews((null!, "Person", "p-1", DateTime.UtcNow.AddHours(-2)));
        var svc = NewService(User(CurrentUserInfo.System));

        Assert.Null(await svc.PreviousVisitAsync("Person", "p-1"));
    }

    public void Dispose() => _ctx.Dispose();
}
