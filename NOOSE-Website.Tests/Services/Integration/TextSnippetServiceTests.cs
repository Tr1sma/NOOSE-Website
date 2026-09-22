using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="TextSnippetService"/> against in-memory SQLite.</summary>
public sealed class TextSnippetServiceTests
{
    private static TextSnippetService NewService(SqliteTestContext ctx) => new(ctx.Factory);

    // An ordinary writing agent; the claim id is what every row must end up carrying.
    private static ClaimsPrincipal Me(string id = "me")
        => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SpecialAgent).WithCodename("Habicht").Build();

    // Read-only supervision is derived from IsTeamLead && !IsAdmin, so no admin flag here.
    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).WithCodename("Auge").AsTeamLead().Build();

    // External partner: read-only for the same reason, a different flag.
    private static ClaimsPrincipal Partner()
        => ClaimsPrincipalBuilder.Agent("partner1").AsPartner(PartnerAgency.LSPD, PartnerRank.Member).Build();

    private static TextSnippetInput Input(string name, string text, int sorting = 0)
        => new() { Name = name, Text = text, Sorting = sorting };

    private static TextSnippet Row(string id, string agentId, string name, string text, int sorting = 0)
        => new() { Id = id, AgentId = agentId, Name = name, Text = text, Sorting = sorting };

    // ---------- CreateAsync / GetMineAsync ----------

    [Fact]
    public async Task CreateAsync_TrimsAndStampsTheSignedInAgent()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        var created = await svc.CreateAsync(
            Input("  Observation ohne Feststellung  ", "  Keine Feststellung getroffen.  ", 3), Me());

        Assert.Equal("Observation ohne Feststellung", created.Name);
        Assert.Equal("Keine Feststellung getroffen.", created.Text);
        Assert.Equal(3, created.Sorting);
        // the owner comes from the principal; the input type deliberately carries no agent field
        Assert.Equal("me", created.AgentId);

        using var db = ctx.NewContext();
        var stored = Assert.Single(db.Textbausteine.ToList());
        Assert.Equal("me", stored.AgentId);
        Assert.Equal("Observation ohne Feststellung", stored.Name);
        Assert.Equal("Keine Feststellung getroffen.", stored.Text);
    }

    [Fact]
    public async Task CreateAsync_KeepsPlaceholderTokensRaw()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        // placeholders are the payload of a stored snippet; they expand on insert, never on save
        var created = await svc.CreateAsync(Input("Vermerk", "Am {{Datum}} zu {{Name}} befragt."), Me());

        Assert.Equal("Am {{Datum}} zu {{Name}} befragt.", created.Text);
    }

    [Fact]
    public async Task GetMineAsync_ReturnsOwnSnippets_OrderedBySortingThenName()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "B-Baustein", "zwei", sorting: 2));
            db.Textbausteine.Add(Row("s2", "me", "C-Baustein", "eins", sorting: 1));
            db.Textbausteine.Add(Row("s3", "me", "A-Baustein", "eins", sorting: 1));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var mine = await svc.GetMineAsync(Me());

        Assert.Equal(new[] { "A-Baustein", "C-Baustein", "B-Baustein" }, mine.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task GetMineAsync_WithoutAnAgentClaim_ReturnsEmpty()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "Vermerk", "Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        var mine = await svc.GetMineAsync(ClaimsPrincipalBuilder.Anonymous());

        Assert.Empty(mine);
    }

    // ---------- The foreign collection ----------

    [Fact]
    public async Task ForeignCollection_IsUnreadable_UneditableAndUndeletable()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("fremd-1", "other", "Fremder Baustein", "Nicht anfassen.", sorting: 7));
            db.SaveChanges();
        }
        var svc = NewService(ctx);
        var me = Me();

        // reading never crosses the owner line
        Assert.Empty(await svc.GetMineAsync(me));

        // and neither does writing: the owner is part of the lookup, so a foreign id finds nothing
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefreshAsync("fremd-1", Input("Gekapert", "Übernommen."), me));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAsync("fremd-1", me));

        using var db2 = ctx.NewContext();
        var stored = Assert.Single(db2.Textbausteine.ToList());
        Assert.Equal("other", stored.AgentId);
        Assert.Equal("Fremder Baustein", stored.Name);
        Assert.Equal("Nicht anfassen.", stored.Text);
        Assert.Equal(7, stored.Sorting);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesOwnSnippet()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "Alt", "Alter Text", sorting: 1));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.RefreshAsync("s1", Input("  Neu  ", "  Neuer Text mit Grüßen.  ", 4), Me());

        using var db2 = ctx.NewContext();
        var stored = Assert.Single(db2.Textbausteine.ToList());
        Assert.Equal("Neu", stored.Name);
        Assert.Equal("Neuer Text mit Grüßen.", stored.Text);
        Assert.Equal(4, stored.Sorting);
        Assert.Equal("me", stored.AgentId);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOwnSnippetForGood()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "Weg damit", "Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await svc.DeleteAsync("s1", Me());

        using var db2 = ctx.NewContext();
        // hard delete on purpose: no ISoftDelete, so nothing lingers in the leadership-readable bin
        Assert.Empty(db2.Textbausteine.ToList());
    }

    // ---------- Duplicate names ----------

    [Fact]
    public async Task CreateAsync_DuplicateName_IsRejected()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);
        var me = Me();
        await svc.CreateAsync(Input("Vermerk", "Erster Text"), me);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAsync(Input("  Vermerk  ", "Zweiter Text"), me));

        // the trimmed name is what collides, not the typed one
        Assert.Contains("Vermerk", ex.Message);
        using var db = ctx.NewContext();
        Assert.Single(db.Textbausteine.ToList());
    }

    [Fact]
    public async Task RefreshAsync_RenamingOntoAnotherOwnSnippet_IsRejected()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "Vermerk", "Erster Text"));
            db.Textbausteine.Add(Row("s2", "me", "Observation", "Zweiter Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefreshAsync("s2", Input("Vermerk", "Zweiter Text"), Me()));

        using var db2 = ctx.NewContext();
        Assert.Equal("Observation", db2.Textbausteine.Single(s => s.Id == "s2").Name);
    }

    [Fact]
    public async Task RefreshAsync_KeepingItsOwnName_IsAllowed()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "Vermerk", "Alter Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        // the duplicate check excludes the row being edited, otherwise no text could ever be changed
        await svc.RefreshAsync("s1", Input("Vermerk", "Neuer Text"), Me());

        using var db2 = ctx.NewContext();
        Assert.Equal("Neuer Text", db2.Textbausteine.Single(s => s.Id == "s1").Text);
    }

    [Fact]
    public async Task CreateAsync_SameNameForAnotherAgent_IsAllowed()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("fremd-1", "other", "Vermerk", "Fremder Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        // the unique index is (AgentId, Bezeichnung): two agents may both keep a "Vermerk"
        var created = await svc.CreateAsync(Input("Vermerk", "Mein Text"), Me());

        Assert.Equal("me", created.AgentId);
        using var db2 = ctx.NewContext();
        Assert.Equal(2, db2.Textbausteine.Count());
    }

    // ---------- Empty input ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithoutAName_IsRejected(string name)
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Input(name, "Text"), Me()));

        using var db = ctx.NewContext();
        Assert.Empty(db.Textbausteine.ToList());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithoutText_IsRejected(string text)
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Input("Vermerk", text), Me()));

        using var db = ctx.NewContext();
        Assert.Empty(db.Textbausteine.ToList());
    }

    [Fact]
    public async Task RefreshAsync_WithEmptyText_IsRejected_AndLeavesTheRowAlone()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "me", "Vermerk", "Alter Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RefreshAsync("s1", Input("Vermerk", "  "), Me()));

        using var db2 = ctx.NewContext();
        Assert.Equal("Alter Text", db2.Textbausteine.Single(s => s.Id == "s1").Text);
    }

    // ---------- The cap ----------

    [Fact]
    public async Task CreateAsync_StopsAtMaxPerAgent()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            for (var i = 0; i < TextSnippetService.MaxPerAgent - 1; i++)
            {
                db.Textbausteine.Add(Row($"s{i}", "me", $"Baustein {i:000}", "Text"));
            }
            // a foreign collection must not count against mine
            db.Textbausteine.Add(Row("fremd-1", "other", "Fremd", "Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);
        var me = Me();

        // the last free slot still works
        await svc.CreateAsync(Input("Letzter Baustein", "Text"), me);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAsync(Input("Einer zu viel", "Text"), me));

        Assert.Contains(TextSnippetService.MaxPerAgent.ToString(), ex.Message);
        using var db2 = ctx.NewContext();
        Assert.Equal(TextSnippetService.MaxPerAgent, db2.Textbausteine.Count(s => s.AgentId == "me"));
    }

    // ---------- Who may write ----------

    [Fact]
    public async Task OnlyReader_MayNotCreateChangeOrDelete()
    {
        using var ctx = new SqliteTestContext();
        using (var db = ctx.NewContext())
        {
            db.Textbausteine.Add(Row("s1", "aufsicht", "Vermerk", "Text"));
            db.SaveChanges();
        }
        var svc = NewService(ctx);
        var aufsicht = OnlyReader();

        // SqliteTestContext attaches no interceptors, so this really measures Permission.RequireWriteAccess
        // and not the ReadOnlyBarrierInterceptor that would stop the save in production.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CreateAsync(Input("Neu", "Text"), aufsicht));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.RefreshAsync("s1", Input("Neu", "Text"), aufsicht));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteAsync("s1", aufsicht));

        using var db2 = ctx.NewContext();
        var stored = Assert.Single(db2.Textbausteine.ToList());
        Assert.Equal("Vermerk", stored.Name);
        Assert.Equal("Text", stored.Text);
    }

    [Fact]
    public async Task Partner_MayNotCreate()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CreateAsync(Input("Neu", "Text"), Partner()));

        using var db = ctx.NewContext();
        Assert.Empty(db.Textbausteine.ToList());
    }

    [Fact]
    public async Task CreateAsync_WithoutAnAgentClaim_IsRejected()
    {
        using var ctx = new SqliteTestContext();
        var svc = NewService(ctx);

        // no NameIdentifier => there is no collection to write into, and none is ever named from outside
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateAsync(Input("Neu", "Text"), ClaimsPrincipalBuilder.Anonymous()));
    }
}
