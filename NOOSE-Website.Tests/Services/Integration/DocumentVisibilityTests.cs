using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The three layers of the document gate, and that the point-check applies all three.</summary>
/// <remarks>Before this helper existed, <see cref="Visibility.IsRecordVisibleAsync"/> applied the secrecy level
/// alone — so a taskforce-internal document, or one an agent was excluded from, stayed reachable through its
/// comments, sources, followups, links, custom fields, requests and the NOOSEI record anchor.</remarks>
public class DocumentVisibilityTests
{
    private static DocumentViewerScope Plain(string? meId = "me")
        => new(MayClassified: false, IsTru: false, IsHrb: false, IsLeadership: false, IsAdmin: false, MeId: meId);

    private static DocumentViewerScope Leadership(string? meId = "lead")
        => new(MayClassified: true, IsTru: false, IsHrb: false, IsLeadership: true, IsAdmin: false, MeId: meId);

    private static DocumentViewerScope Admin(string? meId = "admin")
        => new(MayClassified: true, IsTru: false, IsHrb: false, IsLeadership: true, IsAdmin: true, MeId: meId);

    private static Document Doc(string id, Action<Document>? configure = null)
    {
        var d = new Document
        {
            Id = id, Title = "Dokument " + id, ContentHtml = "<p>x</p>",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        configure?.Invoke(d);
        return d;
    }

    // ---- layer 1: secrecy level ----

    [Fact]
    public async Task Plain_agent_does_not_see_a_leadership_document()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("open"));
            db.Documents.Add(Doc("vs", d => d.IsClassified = true));
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        Assert.False(await DocumentVisibility.IsVisibleAsync(read, "vs", Plain()));
        Assert.True(await DocumentVisibility.IsVisibleAsync(read, "open", Plain()));
    }

    // ---- layer 2: owning taskforce ----

    [Fact]
    public async Task A_taskforce_owned_document_is_invisible_to_a_non_member()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("tf", d => d.OwnerTaskforceId = "t1"));
            db.TaskforceAgents.Add(new TaskforceAgent { TaskforceId = "t1", AgentId = "member" });
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        Assert.False(await DocumentVisibility.IsVisibleAsync(read, "tf", Plain("outsider")));
        Assert.True(await DocumentVisibility.IsVisibleAsync(read, "tf", Plain("member")));
    }

    [Fact]
    public async Task Leadership_reaches_a_taskforce_owned_document_without_membership()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("tf", d => d.OwnerTaskforceId = "t1"));
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        Assert.True(await DocumentVisibility.IsVisibleAsync(read, "tf", Leadership()));
    }

    // ---- layer 3: per-agent revocation ----

    [Fact]
    public async Task An_excluded_agent_loses_a_document_they_would_otherwise_see()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("open"));
            db.DocumentAccessExclusions.Add(new DocumentAccessExclusion { DocumentId = "open", AgentId = "me" });
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        Assert.False(await DocumentVisibility.IsVisibleAsync(read, "open", Plain("me")));
        Assert.True(await DocumentVisibility.IsVisibleAsync(read, "open", Plain("someone-else")));
    }

    [Fact]
    public async Task An_admin_keeps_access_despite_an_exclusion()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("open"));
            db.DocumentAccessExclusions.Add(new DocumentAccessExclusion { DocumentId = "open", AgentId = "admin" });
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        Assert.True(await DocumentVisibility.IsVisibleAsync(read, "open", Admin()));
    }

    // ---- the point-check now applies all three (this is the fix) ----

    [Theory]
    [InlineData("tf")]
    [InlineData("excluded")]
    public async Task IsRecordVisibleAsync_applies_the_taskforce_and_exclusion_layers_too(string documentId)
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("tf", d => d.OwnerTaskforceId = "t1"));
            db.Documents.Add(Doc("excluded"));
            db.DocumentAccessExclusions.Add(new DocumentAccessExclusion { DocumentId = "excluded", AgentId = "me" });
            await db.SaveChangesAsync();
        }
        var scope = new ViewerScope(false, false, "me", null);

        await using var read = ctx.NewContext();
        // both are unclassified, so the secrecy layer alone would have said "visible"
        Assert.False(await Visibility.IsRecordVisibleAsync(read, nameof(Document), documentId, scope));
    }

    [Fact]
    public async Task IsRecordVisibleAsync_still_admits_an_ordinary_document()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("open"));
            await db.SaveChangesAsync();
        }
        var scope = new ViewerScope(false, false, "me", null);

        await using var read = ctx.NewContext();
        Assert.True(await Visibility.IsRecordVisibleAsync(read, nameof(Document), "open", scope));
    }

    [Fact]
    public async Task The_bool_shim_fails_closed_on_the_exclusion_layer()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("open"));
            db.DocumentAccessExclusions.Add(new DocumentAccessExclusion { DocumentId = "open", AgentId = "me" });
            await db.SaveChangesAsync();
        }

        await using var read = ctx.NewContext();
        // the shim carries no admin flag, so the exclusion applies — narrowing, which is the safe direction
        Assert.False(await Visibility.IsRecordVisibleAsync(read, nameof(Document), "open", isLeadership: true, meId: "me"));
    }

    // ---- level reading is the document one, not the record one ----

    [Theory]
    [InlineData(true, false, false, DocumentClassification.Leadership)]
    [InlineData(false, true, false, DocumentClassification.Tru)]
    [InlineData(false, false, true, DocumentClassification.Hrb)]
    [InlineData(false, false, false, DocumentClassification.None)]
    public void LevelOf_reads_IsClassified_as_leadership_exclusive(bool classified, bool tru, bool hrb, DocumentClassification expected)
    {
        // the opposite of RecordVisibility.LevelOf, where IsClassified means "restricted at all"
        Assert.Equal(expected, DocumentVisibility.LevelOf(classified, tru, hrb));
    }

    // ---- batch twin agrees with the point-check ----

    [Fact]
    public async Task VisibleIds_agrees_with_the_point_check_for_every_document()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            db.Documents.Add(Doc("open"));
            db.Documents.Add(Doc("vs", d => d.IsClassified = true));
            db.Documents.Add(Doc("tf", d => d.OwnerTaskforceId = "t1"));
            db.Documents.Add(Doc("excluded"));
            db.DocumentAccessExclusions.Add(new DocumentAccessExclusion { DocumentId = "excluded", AgentId = "me" });
            await db.SaveChangesAsync();
        }
        var ids = new[] { "open", "vs", "tf", "excluded" };

        await using var read = ctx.NewContext();
        var batch = await DocumentVisibility.VisibleIdsAsync(read, ids, Plain());
        foreach (var id in ids)
        {
            Assert.Equal(await DocumentVisibility.IsVisibleAsync(read, id, Plain()), batch.Contains(id));
        }
        Assert.Equal(new[] { "open" }, batch.ToArray());
    }
}
