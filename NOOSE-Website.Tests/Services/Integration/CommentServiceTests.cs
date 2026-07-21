using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="CommentService"/> against in-memory SQLite.</summary>
public sealed class CommentServiceTests
{
    private static (CommentService Svc, INotificationService Notifications) Build(SqliteTestContext ctx)
    {
        var notifications = Substitute.For<INotificationService>();
        return (new CommentService(ctx.Factory, notifications), notifications);
    }

    // Rank >= SupervisorySpecialAgent(4) or admin => IsLeadership() + MayClassifiedRead().
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    // Junior agent: not leadership, cannot read classified records.
    private static ClaimsPrincipal Junior(string id = "junior")
        => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.JuniorAgent).WithCodename("Rookie").Build();

    // External partner: read-only, sees only shared, non-classified records.
    private static ClaimsPrincipal Partner()
        => ClaimsPrincipalBuilder.Agent("partner1").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

    private static Comment MakeComment(string entityType, string entityId, string text,
        DateTime createdAt, string? createdById = null)
        => new()
        {
            EntityType = entityType,
            EntityId = entityId,
            Text = text,
            CreatedAt = createdAt,
            CreatedById = createdById,
        };

    // ---------- GetForRecordAsync (ViewerScope overload) ----------

    [Fact]
    public async Task GetForRecordAsync_Scope_ReturnsComments_NewestFirst()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1"));
            db.Comments.Add(MakeComment("Person", "p1", "older", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.Comments.Add(MakeComment("Person", "p1", "newer", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
            // a comment on a different record must be excluded
            db.Comments.Add(MakeComment("Person", "other", "elsewhere", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // non-classified record is visible even to a junior agent
        var result = await svc.GetForRecordAsync("Person", "p1", ViewerScope.From(Junior()));

        Assert.Equal(new[] { "newer", "older" }, result.Select(c => c.Text).ToArray());
    }

    [Fact]
    public async Task GetForRecordAsync_Scope_ReturnsEmpty_WhenRecordNotVisible()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p2", configure: p => p.IsClassified = true));
            db.Comments.Add(MakeComment("Person", "p2", "secret", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // classified record: junior cannot read it -> empty, regardless of stored comments
        var result = await svc.GetForRecordAsync("Person", "p2", ViewerScope.From(Junior()));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetForRecordAsync_Scope_Partner_ReturnsComments_WhenParentSharedWhole()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p3"));
            db.Comments.Add(MakeComment("Person", "p3", "shared", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            // whole-record release to the whole agency (PartnerAgentId null), children included
            db.PartnerShares.Add(new PartnerShare
            {
                EntityType = "Person",
                EntityId = "p3",
                Agency = PartnerAgency.LSPD,
                PartnerAgentId = null,
                IncludesChildren = true,
            });
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p3", ViewerScope.From(Partner()));

        Assert.Single(result);
        Assert.Equal("shared", result[0].Text);
    }

    [Fact]
    public async Task GetForRecordAsync_Scope_Partner_ReturnsEmpty_WhenChildrenNotReleased()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p4"));
            db.Comments.Add(MakeComment("Person", "p4", "hidden", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            // shell-only release: parent visible, but comments are not individually released
            db.PartnerShares.Add(new PartnerShare
            {
                EntityType = "Person",
                EntityId = "p4",
                Agency = PartnerAgency.LSPD,
                PartnerAgentId = null,
                IncludesChildren = false,
            });
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p4", ViewerScope.From(Partner()));

        Assert.Empty(result);
    }

    // ---------- GetForRecordAsync (bool overload) ----------

    [Fact]
    public async Task GetForRecordAsync_Bool_ReturnsComments_WhenLeadershipSeesClassified()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p5", configure: p => p.IsClassified = true));
            db.Comments.Add(MakeComment("Person", "p5", "vs", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p5", isLeadership: true);

        Assert.Single(result);
        Assert.Equal("vs", result[0].Text);
    }

    [Fact]
    public async Task GetForRecordAsync_Bool_ReturnsEmpty_WhenNonLeadershipOnClassified()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p6", configure: p => p.IsClassified = true));
            db.Comments.Add(MakeComment("Person", "p6", "vs", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        var result = await svc.GetForRecordAsync("Person", "p6", isLeadership: false);

        Assert.Empty(result);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_PersistsTrimmedComment_AndStampsCodename()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p7"));
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        var comment = await svc.CreateAsync("Person", "p7", "  Hallo Welt  ", Junior());

        Assert.Equal("Hallo Welt", comment.Text);
        Assert.Equal("Rookie", comment.AuthorName);
        Assert.Equal("Person", comment.EntityType);
        Assert.Equal("p7", comment.EntityId);

        using var check = ctx.NewContext();
        var stored = await check.Comments.SingleAsync(c => c.EntityId == "p7");
        Assert.Equal("Hallo Welt", stored.Text);
        Assert.Equal("Rookie", stored.AuthorName);
    }

    [Fact]
    public async Task CreateAsync_NotifiesMentions()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p8"));
            db.SaveChanges();
        }
        var (svc, notifications) = Build(ctx);

        await svc.CreateAsync("Person", "p8", "Text mit Erwaehnung", Junior());

        await notifications.Received(1).NotifyMentionedAsync(
            Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(),
            "Person", "p8", Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_Throws_OnEmptyOrWhitespaceText(string? text)
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        // empty-text guard runs before any DB/visibility work
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAsync("Person", "any", text!, Junior()));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenRecordNotVisible()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person("p10", configure: p => p.IsClassified = true));
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // classified record: junior cannot access it
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateAsync("Person", "p10", "nope", Junior()));

        using var check = ctx.NewContext();
        Assert.False(await check.Comments.AnyAsync(c => c.EntityId == "p10"));
    }

    // ---------- DeleteAsync ----------

    [Fact]
    public async Task DeleteAsync_RemovesComment_WhenAuthor()
    {
        using var ctx = new SqliteTestContext();
        var comment = MakeComment("Person", "p1", "mine", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), createdById: "junior");
        using (var db = ctx.NewContext())
        {
            db.Comments.Add(comment);
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // author (id "junior") may delete their own comment even without leadership
        await svc.DeleteAsync(comment.Id, Junior("junior"));

        using var check = ctx.NewContext();
        // no soft-delete interceptor in tests => hard delete, row is gone from the filtered set
        Assert.False(await check.Comments.AnyAsync(c => c.Id == comment.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesComment_WhenLeadership()
    {
        using var ctx = new SqliteTestContext();
        var comment = MakeComment("Person", "p1", "theirs", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), createdById: "someone-else");
        using (var db = ctx.NewContext())
        {
            db.Comments.Add(comment);
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // leadership may delete any comment
        await svc.DeleteAsync(comment.Id, Leader());

        using var check = ctx.NewContext();
        Assert.False(await check.Comments.AnyAsync(c => c.Id == comment.Id));
    }

    [Fact]
    public async Task DeleteAsync_NoOp_WhenCommentMissing()
    {
        using var ctx = new SqliteTestContext();
        var comment = MakeComment("Person", "p1", "keep", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), createdById: "x");
        using (var db = ctx.NewContext())
        {
            db.Comments.Add(comment);
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // unknown id returns silently, touches nothing
        await svc.DeleteAsync("missing", Leader());

        using var check = ctx.NewContext();
        Assert.Equal(1, await check.Comments.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenNotAuthorNorLeadership()
    {
        using var ctx = new SqliteTestContext();
        var comment = MakeComment("Person", "p1", "protected", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), createdById: "other");
        using (var db = ctx.NewContext())
        {
            db.Comments.Add(comment);
            db.SaveChanges();
        }
        var (svc, _) = Build(ctx);

        // junior "junior" is neither the author ("other") nor leadership
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeleteAsync(comment.Id, Junior("junior")));

        using var check = ctx.NewContext();
        Assert.True(await check.Comments.AnyAsync(c => c.Id == comment.Id));
    }
}
