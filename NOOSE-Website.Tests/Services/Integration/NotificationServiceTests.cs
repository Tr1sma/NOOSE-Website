using System.Security.Claims;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="NotificationService"/> over in-memory SQLite.</summary>
public class NotificationServiceTests
{
    // NotificationService has NO Permission.Require* guards; principal-based methods no-op on
    // anonymous rather than throwing, so negative coverage exercises those gates instead.

    private static (NotificationService Svc, NotificationBroadcaster Broadcaster, IDiscordWebhookService Discord, List<string> Reported)
        Build(SqliteTestContext ctx)
    {
        var broadcaster = new NotificationBroadcaster();
        var reported = new List<string>();
        broadcaster.Received += id => reported.Add(id);
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = new NotificationService(ctx.Factory, broadcaster, discord);
        return (svc, broadcaster, discord, reported);
    }

    private static Notification Notif(string recipientId, DateTime createdAt, DateTime? readAt = null,
        string title = "t", NotificationType type = NotificationType.Account)
        => new()
        {
            RecipientId = recipientId,
            Type = type,
            Title = title,
            CreatedAt = createdAt,
            ReadAt = readAt,
        };

    // ---------- NotifyAsync ----------

    [Fact]
    public async Task NotifyAsync_CreatesNotification_AndReports()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, reported) = Build(ctx);

        await svc.NotifyAsync("r1", NotificationType.JobAssigned, "New task", "/aufgaben/1");

        using var db = ctx.NewContext();
        var row = Assert.Single(db.Notifications.ToList());
        Assert.Equal("r1", row.RecipientId);
        Assert.Equal(NotificationType.JobAssigned, row.Type);
        Assert.Equal("New task", row.Title);
        Assert.Equal("/aufgaben/1", row.Href);
        Assert.Null(row.ReadAt);
        Assert.Equal(new[] { "r1" }, reported);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotifyAsync_EmptyRecipient_NoOp(string? recipientId)
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, reported) = Build(ctx);

        await svc.NotifyAsync(recipientId, NotificationType.Account, "x", null);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        Assert.Empty(reported);
    }

    // ---------- NotifyMentionedAsync ----------

    [Fact]
    public async Task NotifyMentionedAsync_MentionedActiveVisible_CreatesNotification_AndPushes()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, reported) = Build(ctx);

        var recipientId = Guid.NewGuid().ToString();
        var triggerId = Guid.NewGuid().ToString();
        using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent(recipientId, Rank.SupervisorySpecialAgent, AgentStatus.Active));
            db.SaveChanges();
        }

        var text = MentionParser.Token("Agent", recipientId);
        var trigger = ClaimsPrincipalBuilder.Agent(triggerId).Build();

        // unknown target type => Visibility returns true regardless of scope
        await svc.NotifyMentionedAsync(text, "You were mentioned", "/personen/1", "SomethingElse", "t1", trigger);

        using (var db = ctx.NewContext())
        {
            var row = Assert.Single(db.Notifications.ToList());
            Assert.Equal(recipientId, row.RecipientId);
            Assert.Equal(NotificationType.Mention, row.Type);
            Assert.Equal("You were mentioned", row.Title);
        }
        Assert.Equal(new[] { recipientId }, reported);
        await discord.Received(1).PushAsync(
            NotificationType.Mention, "/personen/1",
            Arg.Is<IReadOnlyCollection<string>>(c => c.Contains(recipientId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_NoMentions_NoOp()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, _) = Build(ctx);

        var trigger = ClaimsPrincipalBuilder.Agent(Guid.NewGuid().ToString()).Build();
        await svc.NotifyMentionedAsync("plain text, no tokens", "t", "/h", "SomethingElse", "t1", trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_ExcludesTrigger()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, _) = Build(ctx);

        var triggerId = Guid.NewGuid().ToString();
        using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent(triggerId, Rank.SupervisorySpecialAgent, AgentStatus.Active));
            db.SaveChanges();
        }

        // text mentions only the trigger themselves -> excluded -> no-op
        var text = MentionParser.Token("Agent", triggerId);
        var trigger = ClaimsPrincipalBuilder.Agent(triggerId).Build();
        await svc.NotifyMentionedAsync(text, "t", "/h", "SomethingElse", "t1", trigger);

        using var db2 = ctx.NewContext();
        Assert.Empty(db2.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_InactiveRecipient_Skipped()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, _) = Build(ctx);

        var recipientId = Guid.NewGuid().ToString();
        using (var db = ctx.NewContext())
        {
            db.Users.Add(Seed.Agent(recipientId, Rank.SupervisorySpecialAgent, AgentStatus.Pending));
            db.SaveChanges();
        }

        var text = MentionParser.Token("Agent", recipientId);
        var trigger = ClaimsPrincipalBuilder.Agent(Guid.NewGuid().ToString()).Build();
        await svc.NotifyMentionedAsync(text, "t", "/h", "SomethingElse", "t1", trigger);

        using var db2 = ctx.NewContext();
        Assert.Empty(db2.Notifications.ToList());
    }

    [Fact]
    public async Task NotifyMentionedAsync_ClassifiedTargetNonLeadership_Skipped()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, _) = Build(ctx);

        var recipientId = Guid.NewGuid().ToString();
        var person = Seed.Person(configure: p => p.IsClassified = true);
        using (var db = ctx.NewContext())
        {
            // non-leadership recipient (JuniorAgent, not admin) cannot see classified record
            db.Users.Add(Seed.Agent(recipientId, Rank.JuniorAgent, AgentStatus.Active));
            db.People.Add(person);
            db.SaveChanges();
        }

        var text = MentionParser.Token("Agent", recipientId);
        var trigger = ClaimsPrincipalBuilder.Agent(Guid.NewGuid().ToString()).Build();
        await svc.NotifyMentionedAsync(text, "t", "/h", nameof(Person), person.Id, trigger);

        using var db2 = ctx.NewContext();
        Assert.Empty(db2.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_ClassifiedTargetLeadership_Notified()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, reported) = Build(ctx);

        var recipientId = Guid.NewGuid().ToString();
        var person = Seed.Person(configure: p => p.IsClassified = true);
        using (var db = ctx.NewContext())
        {
            // leadership recipient (SupervisorySpecialAgent) sees classified record
            db.Users.Add(Seed.Agent(recipientId, Rank.SupervisorySpecialAgent, AgentStatus.Active));
            db.People.Add(person);
            db.SaveChanges();
        }

        var text = MentionParser.Token("Agent", recipientId);
        var trigger = ClaimsPrincipalBuilder.Agent(Guid.NewGuid().ToString()).Build();
        await svc.NotifyMentionedAsync(text, "t", "/h", nameof(Person), person.Id, trigger);

        using var db2 = ctx.NewContext();
        var row = Assert.Single(db2.Notifications.ToList());
        Assert.Equal(recipientId, row.RecipientId);
        Assert.Equal(NotificationType.Mention, row.Type);
        Assert.Equal(new[] { recipientId }, reported);
        await discord.Received(1).PushAsync(
            NotificationType.Mention, "/h",
            Arg.Is<IReadOnlyCollection<string>>(c => c.Contains(recipientId)),
            Arg.Any<CancellationToken>());
    }

    // ---------- NotifyManyAsync ----------

    [Fact]
    public async Task NotifyManyAsync_CreatesForAll_AndPushes()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, reported) = Build(ctx);

        await svc.NotifyManyAsync(new[] { "a", "b" }, NotificationType.Announcement, "News", "/brett/1", "trigger");

        using var db = ctx.NewContext();
        var rows = db.Notifications.ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(NotificationType.Announcement, r.Type);
            Assert.Equal("News", r.Title);
            Assert.Equal("/brett/1", r.Href);
        });
        Assert.Contains("a", rows.Select(r => r.RecipientId));
        Assert.Contains("b", rows.Select(r => r.RecipientId));
        Assert.Equal(2, reported.Count);
        await discord.Received(1).PushAsync(
            NotificationType.Announcement, "/brett/1",
            Arg.Is<IReadOnlyCollection<string>>(c => c.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyManyAsync_ExcludesTriggerAndDedupes()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, reported) = Build(ctx);

        // duplicates, the trigger, and blank ids are all dropped -> only "a" survives
        await svc.NotifyManyAsync(new[] { "a", "a", "c", " ", "" }, NotificationType.Account, "x", null, "c");

        using var db = ctx.NewContext();
        var row = Assert.Single(db.Notifications.ToList());
        Assert.Equal("a", row.RecipientId);
        Assert.Equal(new[] { "a" }, reported);
        await discord.Received(1).PushAsync(
            NotificationType.Account, null,
            Arg.Is<IReadOnlyCollection<string>>(c => c.Count == 1 && c.Contains("a")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyManyAsync_EmptyAfterFilter_NoOp()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, discord, reported) = Build(ctx);

        await svc.NotifyManyAsync(new[] { "c" }, NotificationType.Account, "x", null, "c");

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        Assert.Empty(reported);
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyCollection<string>?>(), Arg.Any<CancellationToken>());
    }

    // ---------- GetOwnAsync ----------

    [Fact]
    public async Task GetOwnAsync_ReturnsOwnNewestFirst()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, _) = Build(ctx);

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(Notif("me", t0, title: "oldest"));
            db.Notifications.Add(Notif("me", t0.AddHours(2), title: "newest"));
            db.Notifications.Add(Notif("me", t0.AddHours(1), title: "middle"));
            db.Notifications.Add(Notif("someone-else", t0.AddHours(3), title: "other"));
            db.SaveChanges();
        }

        var actor = ClaimsPrincipalBuilder.Agent("me").Build();
        var result = await svc.GetOwnAsync(actor);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "newest", "middle", "oldest" }, result.Select(n => n.Title));
        Assert.DoesNotContain(result, n => n.RecipientId == "someone-else");
    }

    [Fact]
    public async Task GetOwnAsync_RespectsMaxClamp()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, _) = Build(ctx);

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var db = ctx.NewContext())
        {
            for (var i = 0; i < 3; i++)
            {
                db.Notifications.Add(Notif("me", t0.AddHours(i), title: $"n{i}"));
            }
            db.SaveChanges();
        }

        var actor = ClaimsPrincipalBuilder.Agent("me").Build();
        var result = await svc.GetOwnAsync(actor, 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "n2", "n1" }, result.Select(n => n.Title));
    }

    [Fact]
    public async Task GetOwnAsync_Anonymous_ReturnsEmpty()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(Notif("me", DateTime.UtcNow));
            db.SaveChanges();
        }

        var result = await svc.GetOwnAsync(ClaimsPrincipalBuilder.Anonymous());
        Assert.Empty(result);
    }

    // ---------- GetUnreadCountAsync ----------

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadForCaller()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, _) = Build(ctx);

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(Notif("me", t0));                       // unread
            db.Notifications.Add(Notif("me", t0));                       // unread
            db.Notifications.Add(Notif("me", t0, readAt: t0.AddDays(1))); // read
            db.Notifications.Add(Notif("other", t0));                     // other agent, unread
            db.SaveChanges();
        }

        var actor = ClaimsPrincipalBuilder.Agent("me").Build();
        Assert.Equal(2, await svc.GetUnreadCountAsync(actor));
    }

    [Fact]
    public async Task GetUnreadCountAsync_Anonymous_ReturnsZero()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(Notif("me", DateTime.UtcNow));
            db.SaveChanges();
        }

        Assert.Equal(0, await svc.GetUnreadCountAsync(ClaimsPrincipalBuilder.Anonymous()));
    }

    // ---------- AsReadMarkAsync ----------

    [Fact]
    public async Task AsReadMarkAsync_OwnUnread_SetsReadAt_AndReports()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, reported) = Build(ctx);

        var n = Notif("me", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(n);
            db.SaveChanges();
        }

        var actor = ClaimsPrincipalBuilder.Agent("me").Build();
        await svc.AsReadMarkAsync(n.Id, actor);

        using var db2 = ctx.NewContext();
        var row = db2.Notifications.Single(x => x.Id == n.Id);
        Assert.NotNull(row.ReadAt);
        Assert.Equal(new[] { "me" }, reported);
    }

    [Fact]
    public async Task AsReadMarkAsync_NotOwn_NoChange()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, reported) = Build(ctx);

        var n = Notif("other", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(n);
            db.SaveChanges();
        }

        var actor = ClaimsPrincipalBuilder.Agent("me").Build();
        await svc.AsReadMarkAsync(n.Id, actor);

        using var db2 = ctx.NewContext();
        var row = db2.Notifications.Single(x => x.Id == n.Id);
        Assert.Null(row.ReadAt);
        Assert.Empty(reported);
    }

    // ---------- AllAsReadAsync ----------

    [Fact]
    public async Task AllAsReadAsync_MarksAllOwnUnread_LeavesOthers()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, reported) = Build(ctx);

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var otherUnread = Notif("other", t0);
        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(Notif("me", t0));
            db.Notifications.Add(Notif("me", t0));
            db.Notifications.Add(otherUnread);
            db.SaveChanges();
        }

        var actor = ClaimsPrincipalBuilder.Agent("me").Build();
        await svc.AllAsReadAsync(actor);

        using var db2 = ctx.NewContext();
        Assert.Equal(0, db2.Notifications.Count(n => n.RecipientId == "me" && n.ReadAt == null));
        Assert.Equal(2, db2.Notifications.Count(n => n.RecipientId == "me" && n.ReadAt != null));
        Assert.Null(db2.Notifications.Single(n => n.Id == otherUnread.Id).ReadAt);
        Assert.Equal(new[] { "me" }, reported);
    }

    [Fact]
    public async Task AllAsReadAsync_Anonymous_NoOp()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _, reported) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Notifications.Add(Notif("me", DateTime.UtcNow));
            db.SaveChanges();
        }

        await svc.AllAsReadAsync(ClaimsPrincipalBuilder.Anonymous());

        using var db2 = ctx.NewContext();
        Assert.Equal(1, db2.Notifications.Count(n => n.ReadAt == null));
        Assert.Empty(reported);
    }
}
