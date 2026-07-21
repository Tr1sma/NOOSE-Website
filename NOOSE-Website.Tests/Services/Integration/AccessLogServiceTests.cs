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

    public void Dispose() => _ctx.Dispose();
}
