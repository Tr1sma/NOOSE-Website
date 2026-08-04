using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Gamification;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="TopAgentAwardService"/> (schedule guard, Discord post, personnel entries).</summary>
public sealed class TopAgentAwardServiceTests
{
    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
    private static ClaimsPrincipal Junior() => ClaimsPrincipalBuilder.Agent("low").WithRank(Rank.JuniorAgent).Build();

    private static List<LeaderboardEntry> Top(params string[] ids)
        => ids.Select((id, i) => new LeaderboardEntry(i + 1, id, $"CN-{id}", 100 - i, 5, 2, 1, 1)).ToList();

    private static void SeedConfig(SqliteTestContext ctx, bool enabled, int interval, bool note, DateTime? lastRun = null)
    {
        using var db = ctx.NewContext();
        db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.BestAgentEnabled, Value = enabled ? "true" : "false" });
        db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.BestAgentIntervalDays, Value = interval.ToString(CultureInfo.InvariantCulture) });
        db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.BestAgentCreateNote, Value = note ? "true" : "false" });
        if (lastRun is not null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.BestAgentLastRun, Value = lastRun.Value.ToString("o", CultureInfo.InvariantCulture) });
        }
        db.SaveChanges();
    }

    private static (TopAgentAwardService Svc, IDiscordWebhookService Discord) New(SqliteTestContext ctx, IReadOnlyList<LeaderboardEntry> top)
    {
        var gam = Substitute.For<IGamificationService>();
        gam.GetLeaderboardAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(top);
        var discord = Substitute.For<IDiscordWebhookService>();
        discord.PushCustomAsync(Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return (new TopAgentAwardService(ctx.Factory, gam, discord), discord);
    }

    [Fact]
    public async Task RunDue_Disabled_DoesNothing()
    {
        using var ctx = new SqliteTestContext();
        SeedConfig(ctx, enabled: false, interval: 7, note: true);
        var (svc, discord) = New(ctx, Top("a1", "a2", "a3"));

        var ran = await svc.RunDueAsync(DateTime.UtcNow);

        Assert.False(ran);
        await discord.DidNotReceive().PushCustomAsync(Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunDue_IntervalNotElapsed_Skips()
    {
        using var ctx = new SqliteTestContext();
        SeedConfig(ctx, enabled: true, interval: 7, note: true, lastRun: DateTime.UtcNow.AddDays(-2));
        var (svc, discord) = New(ctx, Top("a1", "a2", "a3"));

        var ran = await svc.RunDueAsync(DateTime.UtcNow);

        Assert.False(ran);
        await discord.DidNotReceive().PushCustomAsync(Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunDue_Elapsed_PostsAnnouncement_FilesNotes_StampsLastRun()
    {
        using var ctx = new SqliteTestContext();
        SeedConfig(ctx, enabled: true, interval: 7, note: true, lastRun: DateTime.UtcNow.AddDays(-10));
        var (svc, discord) = New(ctx, Top("a1", "a2", "a3"));

        var ran = await svc.RunDueAsync(DateTime.UtcNow);

        Assert.True(ran);
        await discord.Received(1).PushCustomAsync(NotificationType.Announcement, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        using var check = ctx.NewContext();
        var notes = await check.AgentNotes.ToListAsync();
        Assert.Equal(3, notes.Count);
        Assert.All(notes, n => Assert.Equal(AgentNoteKind.Commendation, n.Kind));
        Assert.All(notes, n => Assert.Contains("KW", n.Text));
        Assert.NotNull((await check.SystemSettings.SingleAsync(s => s.Key == SystemSettingKeys.BestAgentLastRun)).Value);
    }

    [Fact]
    public async Task RunDue_NoteToggleOff_PostsButFilesNoNotes()
    {
        using var ctx = new SqliteTestContext();
        SeedConfig(ctx, enabled: true, interval: 7, note: false, lastRun: DateTime.UtcNow.AddDays(-10));
        var (svc, discord) = New(ctx, Top("a1", "a2", "a3"));

        await svc.RunDueAsync(DateTime.UtcNow);

        await discord.Received(1).PushCustomAsync(NotificationType.Announcement, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        using var check = ctx.NewContext();
        Assert.Equal(0, await check.AgentNotes.CountAsync());
    }

    [Fact]
    public async Task RunNow_RequiresLeadership()
    {
        using var ctx = new SqliteTestContext();
        SeedConfig(ctx, enabled: true, interval: 7, note: true);
        var (svc, _) = New(ctx, Top("a1"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RunNowAsync(Junior()));
    }

    [Fact]
    public async Task RunNow_TwiceInSameWeek_DoesNotDuplicateNotes()
    {
        using var ctx = new SqliteTestContext();
        SeedConfig(ctx, enabled: true, interval: 7, note: true);
        var (svc, _) = New(ctx, Top("a1"));

        await svc.RunNowAsync(Leader());
        await svc.RunNowAsync(Leader());

        using var check = ctx.NewContext();
        Assert.Equal(1, await check.AgentNotes.CountAsync(n => n.AgentId == "a1"));
    }
}
