using System.Security.Claims;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for NotificationService over in-memory SQLite.</summary>
public class NotificationServiceTests
{
    // NotificationService has NO Permission.Require* guards; principal-based methods no-op on
    // anonymous rather than throwing, so negative coverage exercises those gates instead.

    // GUID-shaped ids required by MentionParser's token regex.
    private const string RecipientGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string TriggerGuid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string PersonGuid = "cccccccc-cccc-cccc-cccc-cccccccccccc";

    private static NotificationService NewService(
        SqliteTestContext ctx, NotificationBroadcaster broadcaster, IDiscordWebhookService discord)
        => new(ctx.Factory, broadcaster, discord);

    private static string MentionToken(string agentId) => $"@{{Agent:{agentId}}}";

    // ---- NotifyAsync -------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_CreatesNotification_AndBroadcasts()
    {
        using var ctx = new SqliteTestContext();
        var broadcasts = new List<string>();
        var broadcaster = new NotificationBroadcaster();
        broadcaster.Received += id => broadcasts.Add(id);
        var svc = NewService(ctx, broadcaster, Substitute.For<IDiscordWebhookService>());

        await svc.NotifyAsync("agent-1", NotificationType.Account, "Konto geändert", "/konto");

        using var db = ctx.NewContext();
        var n = db.Notifications.Single();
        Assert.Equal("agent-1", n.RecipientId);
        Assert.Equal(NotificationType.Account, n.Type);
        Assert.Equal("Konto geändert", n.Title);
        Assert.Equal("/konto", n.Href);
        Assert.Null(n.ReadAt);
        Assert.Equal(new[] { "agent-1" }, broadcasts);
    }

    [Fact]
    public async Task NotifyAsync_EmptyRecipient_IsNoOp()
    {
        using var ctx = new SqliteTestContext();
        var broadcasts = new List<string>();
        var broadcaster = new NotificationBroadcaster();
        broadcaster.Received += id => broadcasts.Add(id);
        var svc = NewService(ctx, broadcaster, Substitute.For<IDiscordWebhookService>());

        await svc.NotifyAsync("   ", NotificationType.Account, "x", null);
        await svc.NotifyAsync(null, NotificationType.Account, "x", null);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        Assert.Empty(broadcasts);
    }

    // ---- NotifyMentionedAsync ----------------------------------------------

    [Fact]
    public async Task NotifyMentionedAsync_VisibleActiveRecipient_GetsMention_AndPushesDiscord()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Users.Add(Seed.Agent(RecipientGuid, rank: Rank.JuniorAgent, status: AgentStatus.Active));
            seed.People.Add(Seed.Person(PersonGuid, "Ziel", p => p.IsClassified = false));
            seed.SaveChanges();
        }
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedAsync(
            $"Hallo {MentionToken(RecipientGuid)}!", "Du wurdest erwähnt", "/personen/x",
            "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        var n = db.Notifications.Single();
        Assert.Equal(RecipientGuid, n.RecipientId);
        Assert.Equal(NotificationType.Mention, n.Type);
        await discord.Received(1).PushAsync(
            NotificationType.Mention, Arg.Any<string>(),
            Arg.Is<IReadOnlyCollection<string>>(c => c.Count == 1 && c.Contains(RecipientGuid)),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_MentionOfTriggerSelf_IsExcluded()
    {
        using var ctx = new SqliteTestContext();
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedAsync(
            $"Ich {MentionToken(TriggerGuid)}", "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_ClassifiedTargetInvisibleToNonLeadership_NoNotification()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Users.Add(Seed.Agent(RecipientGuid, rank: Rank.JuniorAgent, status: AgentStatus.Active));
            seed.People.Add(Seed.Person(PersonGuid, "Geheim", p => p.IsClassified = true));
            seed.SaveChanges();
        }
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedAsync(
            MentionToken(RecipientGuid), "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedAsync_InactiveRecipient_NoNotification()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Users.Add(Seed.Agent(RecipientGuid, rank: Rank.JuniorAgent, status: AgentStatus.Pending));
            seed.People.Add(Seed.Person(PersonGuid, "Ziel", p => p.IsClassified = false));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedAsync(
            MentionToken(RecipientGuid), "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
    }

    [Fact]
    public async Task NotifyMentionedAsync_NoMentionTokens_IsNoOp()
    {
        using var ctx = new SqliteTestContext();
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedAsync("kein token hier", "t", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- NotifyMentionedDeltaAsync -----------------------------------------

    // Seeds one active recipient plus a visible person record, the shared setup for the delta tests.
    private static void SeedRecipientAndTarget(SqliteTestContext ctx, string recipientId = RecipientGuid)
    {
        using var seed = ctx.NewContext();
        seed.Users.Add(Seed.Agent(recipientId, rank: Rank.JuniorAgent, status: AgentStatus.Active));
        seed.People.Add(Seed.Person(PersonGuid, "Ziel", p => p.IsClassified = false));
        seed.SaveChanges();
    }

    [Fact]
    public async Task NotifyMentionedDeltaAsync_NewToken_Notifies()
    {
        using var ctx = new SqliteTestContext();
        SeedRecipientAndTarget(ctx);
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedDeltaAsync(
            "Sachverhalt ohne Erwähnung", $"Sachverhalt mit {MentionToken(RecipientGuid)}",
            "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Equal(RecipientGuid, db.Notifications.Single().RecipientId);
    }

    [Fact]
    public async Task NotifyMentionedDeltaAsync_UnchangedToken_DoesNotNotifyAgain()
    {
        using var ctx = new SqliteTestContext();
        SeedRecipientAndTarget(ctx);
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();
        var token = MentionToken(RecipientGuid);

        // same mention, only surrounding prose edited -> no second ping
        await svc.NotifyMentionedDeltaAsync(
            $"Fassung eins {token}", $"Fassung zwei {token}", "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyMentionedDeltaAsync_RemovedToken_DoesNotNotify()
    {
        using var ctx = new SqliteTestContext();
        SeedRecipientAndTarget(ctx);
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedDeltaAsync(
            $"Mit {MentionToken(RecipientGuid)}", "Ohne", "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
    }

    [Fact]
    public async Task NotifyMentionedDeltaAsync_NullOldText_BehavesLikeFullNotify()
    {
        using var ctx = new SqliteTestContext();
        SeedRecipientAndTarget(ctx);
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedDeltaAsync(
            null, MentionToken(RecipientGuid), "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Equal(RecipientGuid, db.Notifications.Single().RecipientId);
    }

    [Fact]
    public async Task NotifyMentionedDeltaAsync_OnlyTheAddedAgentIsNotified()
    {
        const string SecondGuid = "dddddddd-dddd-dddd-dddd-dddddddddddd";
        using var ctx = new SqliteTestContext();
        SeedRecipientAndTarget(ctx);
        using (var seed = ctx.NewContext())
        {
            seed.Users.Add(Seed.Agent(SecondGuid, rank: Rank.JuniorAgent, status: AgentStatus.Active));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedDeltaAsync(
            MentionToken(RecipientGuid), $"{MentionToken(RecipientGuid)} und {MentionToken(SecondGuid)}",
            "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Equal(SecondGuid, db.Notifications.Single().RecipientId);
    }

    [Fact]
    public async Task NotifyMentionedDeltaAsync_ClassifiedTargetInvisibleToNonLeadership_NoNotification()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Users.Add(Seed.Agent(RecipientGuid, rank: Rank.JuniorAgent, status: AgentStatus.Active));
            seed.People.Add(Seed.Person(PersonGuid, "Geheim", p => p.IsClassified = true));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var trigger = ClaimsPrincipalBuilder.Agent(TriggerGuid).Build();

        await svc.NotifyMentionedDeltaAsync(
            null, MentionToken(RecipientGuid), "Erwähnung", "/x", "Person", PersonGuid, trigger);

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
    }

    // ---- NotifyManyAsync ---------------------------------------------------

    [Fact]
    public async Task NotifyManyAsync_ExcludesTrigger_Dedupes_DropsEmpty_AndPushes()
    {
        using var ctx = new SqliteTestContext();
        var broadcasts = new List<string>();
        var broadcaster = new NotificationBroadcaster();
        broadcaster.Received += id => broadcasts.Add(id);
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, broadcaster, discord);

        var ids = new[] { "r1", "r2", "r1", "trigger", "  ", "" };
        await svc.NotifyManyAsync(ids, NotificationType.Announcement, "Neu", "/brett", "trigger");

        using var db = ctx.NewContext();
        var rows = db.Notifications.ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.RecipientId == "r1");
        Assert.Contains(rows, r => r.RecipientId == "r2");
        Assert.DoesNotContain(rows, r => r.RecipientId == "trigger");
        Assert.All(rows, r => Assert.Equal(NotificationType.Announcement, r.Type));
        Assert.Equal(2, broadcasts.Count);
        await discord.Received(1).PushAsync(
            NotificationType.Announcement, "/brett",
            Arg.Is<IReadOnlyCollection<string>>(c => c.Count == 2),
            Arg.Is<string>(h => h == "Neu"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyManyAsync_EmptyAfterFilter_IsNoOp()
    {
        using var ctx = new SqliteTestContext();
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);

        await svc.NotifyManyAsync(new[] { "trigger", "", "  " }, NotificationType.Account, "t", null, "trigger");

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- NotifyOnceAsync / NotifyManyOnceAsync -----------------------------

    [Fact]
    public async Task NotifyOnceAsync_SecondEvent_FoldsIntoTheUnreadRow()
    {
        using var ctx = new SqliteTestContext();
        var broadcasts = new List<string>();
        var broadcaster = new NotificationBroadcaster();
        broadcaster.Received += id => broadcasts.Add(id);
        var svc = NewService(ctx, broadcaster, Substitute.For<IDiscordWebhookService>());

        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "Erste Notiz", "/tickets/t1");
        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "Zweite Notiz", "/tickets/t1");
        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "Dritte Notiz", "/tickets/t1");

        using var db = ctx.NewContext();
        var row = db.Notifications.Single();
        // the newest event owns the headline, and the bump keeps the folded row at the top of the bell
        Assert.Equal("Dritte Notiz", row.Title);
        Assert.Null(row.ReadAt);
        Assert.True(row.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
        // the bell still refreshes on every event, even though no row was added
        Assert.Equal(3, broadcasts.Count);
    }

    [Fact]
    public async Task NotifyOnceAsync_AfterTheRowWasRead_StartsANewOne()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());
        var actor = ClaimsPrincipalBuilder.Agent("agent-1").Build();

        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "Erste Notiz", "/tickets/t1");
        await svc.AllAsReadAsync(actor);
        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "Zweite Notiz", "/tickets/t1");

        using var db = ctx.NewContext();
        var rows = db.Notifications.ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, await svc.GetUnreadCountAsync(actor));
    }

    [Fact]
    public async Task NotifyOnceAsync_OtherTargetOrCategoryOrRecipient_StaysItsOwnRow()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "a", "/tickets/t1");
        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketInternal, "b", "/tickets/t2");
        await svc.NotifyOnceAsync("agent-1", NotificationType.PublicTicketOpened, "c", "/tickets/t1");
        await svc.NotifyOnceAsync("agent-2", NotificationType.PublicTicketInternal, "d", "/tickets/t1");

        using var db = ctx.NewContext();
        Assert.Equal(4, db.Notifications.Count());
    }

    [Fact]
    public async Task NotifyOnceAsync_WithoutAnHref_AlwaysAddsARow()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        // no target, no identity to fold on
        await svc.NotifyOnceAsync("agent-1", NotificationType.Account, "a", null);
        await svc.NotifyOnceAsync("agent-1", NotificationType.Account, "b", null);

        using var db = ctx.NewContext();
        Assert.Equal(2, db.Notifications.Count());
    }

    [Fact]
    public async Task NotifyOnceAsync_EmptyRecipient_IsNoOp()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        await svc.NotifyOnceAsync("   ", NotificationType.Account, "x", "/tickets/t1");
        await svc.NotifyOnceAsync(null, NotificationType.Account, "x", "/tickets/t1");

        using var db = ctx.NewContext();
        Assert.Empty(db.Notifications.ToList());
    }

    [Fact]
    public async Task NotifyManyOnceAsync_FoldsPerRecipient_AndPushesOnlyTheFreshOnes()
    {
        using var ctx = new SqliteTestContext();
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);

        await svc.NotifyManyOnceAsync(new[] { "r1" }, NotificationType.Announcement, "Neu", "/brett", null);
        discord.ClearReceivedCalls();

        await svc.NotifyManyOnceAsync(new[] { "r1", "r2" }, NotificationType.Announcement, "Nachtrag", "/brett", null);

        using var db = ctx.NewContext();
        var rows = db.Notifications.ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Nachtrag", rows.Single(r => r.RecipientId == "r1").Title);
        // r1 was already announced; only r2 is news to the channel
        await discord.Received(1).PushAsync(
            NotificationType.Announcement, "/brett",
            Arg.Is<IReadOnlyCollection<string>>(c => c.Count == 1 && c.Contains("r2")),
            Arg.Is<string>(h => h == "Nachtrag"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyManyOnceAsync_AllFolded_KeepsTheChannelQuiet()
    {
        using var ctx = new SqliteTestContext();
        var discord = Substitute.For<IDiscordWebhookService>();
        var svc = NewService(ctx, new NotificationBroadcaster(), discord);

        await svc.NotifyManyOnceAsync(new[] { "r1", "r2" }, NotificationType.Announcement, "Neu", "/brett", null);
        discord.ClearReceivedCalls();

        await svc.NotifyManyOnceAsync(new[] { "r1", "r2" }, NotificationType.Announcement, "Nachtrag", "/brett", null);

        using var db = ctx.NewContext();
        Assert.Equal(2, db.Notifications.Count());
        await discord.DidNotReceive().PushAsync(
            Arg.Any<NotificationType>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- GetOwnAsync -------------------------------------------------------

    [Fact]
    public async Task GetOwnAsync_ReturnsOnlyOwn_NewestFirst()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(NewRow("agent-1", "alt", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.Notifications.Add(NewRow("agent-1", "neu", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.Notifications.Add(NewRow("agent-1", "mittel", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.Notifications.Add(NewRow("other", "fremd", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        var result = await svc.GetOwnAsync(ClaimsPrincipalBuilder.Agent("agent-1").Build());

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "neu", "mittel", "alt" }, result.Select(r => r.Title).ToArray());
    }

    [Fact]
    public async Task GetOwnAsync_RespectsMaxClamp()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(NewRow("agent-1", "a", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.Notifications.Add(NewRow("agent-1", "b", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        // max below 1 clamps up to 1; returns only the single newest row
        var result = await svc.GetOwnAsync(ClaimsPrincipalBuilder.Agent("agent-1").Build(), max: 0);

        Assert.Single(result);
        Assert.Equal("b", result[0].Title);
    }

    [Fact]
    public async Task GetOwnAsync_Anonymous_ReturnsEmpty()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(NewRow("agent-1", "a", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        var result = await svc.GetOwnAsync(ClaimsPrincipalBuilder.Anonymous());

        Assert.Empty(result);
    }

    // ---- GetUnreadCountAsync -----------------------------------------------

    [Fact]
    public async Task GetUnreadCountAsync_CountsOnlyOwnUnread()
    {
        using var ctx = new SqliteTestContext();
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(NewRow("agent-1", "u1", t));
            seed.Notifications.Add(NewRow("agent-1", "u2", t));
            seed.Notifications.Add(NewRow("agent-1", "gelesen", t, readAt: t));
            seed.Notifications.Add(NewRow("other", "fremd", t));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        var count = await svc.GetUnreadCountAsync(ClaimsPrincipalBuilder.Agent("agent-1").Build());

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetUnreadCountAsync_Anonymous_ReturnsZero()
    {
        using var ctx = new SqliteTestContext();
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(NewRow("agent-1", "u", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        var count = await svc.GetUnreadCountAsync(ClaimsPrincipalBuilder.Anonymous());

        Assert.Equal(0, count);
    }

    // ---- AsReadMarkAsync ---------------------------------------------------

    [Fact]
    public async Task AsReadMarkAsync_MarksOwnUnread_AndBroadcasts()
    {
        using var ctx = new SqliteTestContext();
        var row = NewRow("agent-1", "u", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(row);
            seed.SaveChanges();
        }
        var broadcasts = new List<string>();
        var broadcaster = new NotificationBroadcaster();
        broadcaster.Received += id => broadcasts.Add(id);
        var svc = NewService(ctx, broadcaster, Substitute.For<IDiscordWebhookService>());

        await svc.AsReadMarkAsync(row.Id, ClaimsPrincipalBuilder.Agent("agent-1").Build());

        using var db = ctx.NewContext();
        Assert.NotNull(db.Notifications.Single(n => n.Id == row.Id).ReadAt);
        Assert.Equal(new[] { "agent-1" }, broadcasts);
    }

    [Fact]
    public async Task AsReadMarkAsync_OthersNotification_IsNoOp()
    {
        using var ctx = new SqliteTestContext();
        var row = NewRow("other", "u", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(row);
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        await svc.AsReadMarkAsync(row.Id, ClaimsPrincipalBuilder.Agent("agent-1").Build());

        using var db = ctx.NewContext();
        Assert.Null(db.Notifications.Single(n => n.Id == row.Id).ReadAt);
    }

    [Fact]
    public async Task AsReadMarkAsync_AlreadyRead_LeavesTimestampUnchanged()
    {
        using var ctx = new SqliteTestContext();
        var already = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var row = NewRow("agent-1", "u", already, readAt: already);
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(row);
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        await svc.AsReadMarkAsync(row.Id, ClaimsPrincipalBuilder.Agent("agent-1").Build());

        using var db = ctx.NewContext();
        Assert.Equal(already, db.Notifications.Single(n => n.Id == row.Id).ReadAt);
    }

    // ---- AllAsReadAsync ----------------------------------------------------

    [Fact]
    public async Task AllAsReadAsync_MarksAllOwnUnread_LeavesOthers()
    {
        using var ctx = new SqliteTestContext();
        var t = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var mine1 = NewRow("agent-1", "m1", t);
        var mine2 = NewRow("agent-1", "m2", t);
        var others = NewRow("other", "o", t);
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.AddRange(mine1, mine2, others);
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        await svc.AllAsReadAsync(ClaimsPrincipalBuilder.Agent("agent-1").Build());

        using var db = ctx.NewContext();
        Assert.NotNull(db.Notifications.Single(n => n.Id == mine1.Id).ReadAt);
        Assert.NotNull(db.Notifications.Single(n => n.Id == mine2.Id).ReadAt);
        Assert.Null(db.Notifications.Single(n => n.Id == others.Id).ReadAt);
    }

    [Fact]
    public async Task AllAsReadAsync_Anonymous_IsNoOp()
    {
        using var ctx = new SqliteTestContext();
        var row = NewRow("agent-1", "u", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using (var seed = ctx.NewContext())
        {
            seed.Notifications.Add(row);
            seed.SaveChanges();
        }
        var svc = NewService(ctx, new NotificationBroadcaster(), Substitute.For<IDiscordWebhookService>());

        await svc.AllAsReadAsync(ClaimsPrincipalBuilder.Anonymous());

        using var db = ctx.NewContext();
        Assert.Null(db.Notifications.Single(n => n.Id == row.Id).ReadAt);
    }

    private static Notification NewRow(string recipientId, string title, DateTime createdAt, DateTime? readAt = null)
        => new()
        {
            RecipientId = recipientId,
            Type = NotificationType.Account,
            Title = title,
            CreatedAt = createdAt,
            ReadAt = readAt,
        };
}
