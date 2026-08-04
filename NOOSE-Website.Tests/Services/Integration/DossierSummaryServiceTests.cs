using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Cached AI dossier summaries: the LLM is called only when the source content changed or a regenerate is forced.</summary>
public sealed class DossierSummaryServiceTests
{
    private const string LlmAnswer = "## TL;DR\nKurze Kernaussage.\n\n## Zusammenfassung\nAusführliche Details hier.\n\n- Punkt eins\n- Punkt zwei";

    private static ILlmService Llm()
    {
        var llm = Substitute.For<ILlmService>();
        llm.IsConfigured.Returns(true);
        llm.Model.Returns("test-model");
        llm.ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(LlmAnswer);
        return llm;
    }

    private static DossierSummaryService Svc(SqliteTestContext ctx, ILlmService llm, bool allowClassified = false)
        => new(ctx.Factory, llm, Options.Create(new LlmOptions
        {
            Enabled = true,
            ApiKey = "k",
            Model = "test-model",
            AllowClassifiedContent = allowClassified,
        }));

    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    private static void SeedPerson(SqliteTestContext ctx, string id, string name, Action<Person>? configure = null)
    {
        using var db = ctx.NewContext();
        db.People.Add(Seed.Person(id: id, name: name, configure: configure));
        db.SaveChanges();
    }

    private static void Rename(SqliteTestContext ctx, string id, string name)
    {
        using var db = ctx.NewContext();
        var p = db.People.First(x => x.Id == id);
        p.Name = name;
        db.SaveChanges();
    }

    // ---- caching behaviour ----

    [Fact]
    public async Task Generate_Twice_UnchangedSource_CallsLlmOnce()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p1", "Max Mustermann");
        var llm = Llm();
        var svc = Svc(ctx, llm);

        var v1 = await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: false);
        var v2 = await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: false);

        await llm.Received(1).ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
        Assert.True(v1.Exists);
        Assert.False(v2.IsStale);
        Assert.Equal("test-model", v2.Model);
        Assert.False(string.IsNullOrWhiteSpace(v2.TldrHtml));
        Assert.False(string.IsNullOrWhiteSpace(v2.SummaryHtml));
    }

    [Fact]
    public async Task Generate_Force_CallsLlmAgain()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p1", "Max Mustermann");
        var llm = Llm();
        var svc = Svc(ctx, llm);

        await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: false);
        await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: true);

        await llm.Received(2).ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_AfterContentChange_CallsLlmAgain()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p1", "Max Mustermann");
        var llm = Llm();
        var svc = Svc(ctx, llm);

        await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: false);
        Rename(ctx, "p1", "Moritz Mustermann");
        await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: false);

        await llm.Received(2).ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_AfterContentChange_MarksStale_WithoutLlmCall()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p1", "Max Mustermann");
        var llm = Llm();
        var svc = Svc(ctx, llm);

        await svc.GenerateAsync(nameof(Person), "p1", Leader(), force: false);
        Rename(ctx, "p1", "Moritz Mustermann");
        var view = await svc.GetAsync(nameof(Person), "p1", Leader());

        Assert.NotNull(view);
        Assert.True(view!.Exists);
        Assert.True(view.IsStale);
        // Get never calls the LLM — still exactly one generation call.
        await llm.Received(1).ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_NoSummaryYet_ReturnsNotExists()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p1", "Max Mustermann");

        var view = await Svc(ctx, Llm()).GetAsync(nameof(Person), "p1", Leader());

        Assert.NotNull(view);
        Assert.False(view!.Exists);
        Assert.True(view.Configured);
    }

    // ---- visibility + classification gates ----

    [Fact]
    public async Task Get_ClassifiedRecord_InvisibleToNonLeadership_ReturnsNull()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p2", "Geheim", p => p.IsClassified = true);
        var junior = ClaimsPrincipalBuilder.Agent("j").WithRank(Rank.JuniorAgent).Build();

        var view = await Svc(ctx, Llm()).GetAsync(nameof(Person), "p2", junior);

        Assert.Null(view);
    }

    [Fact]
    public async Task Generate_ClassifiedRecord_NotAllowed_Throws()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p3", "Geheim", p => p.IsClassified = true);
        var llm = Llm();
        var svc = Svc(ctx, llm, allowClassified: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GenerateAsync(nameof(Person), "p3", Leader(), force: false));
        await llm.DidNotReceive().ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_ClassifiedRecord_Allowed_CallsLlm()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p3", "Geheim", p => p.IsClassified = true);
        var llm = Llm();
        var svc = Svc(ctx, llm, allowClassified: true);

        await svc.GenerateAsync(nameof(Person), "p3", Leader(), force: false);

        await llm.Received(1).ChatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_OnlyReader_Throws()
    {
        using var ctx = new SqliteTestContext();
        SeedPerson(ctx, "p4", "Max Mustermann");
        var onlyReader = ClaimsPrincipalBuilder.Agent("or").AsTeamLead().Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Svc(ctx, Llm()).GenerateAsync(nameof(Person), "p4", onlyReader, force: false));
    }

    // ---- markdown split + render helpers (pure) ----

    [Fact]
    public void Split_ParsesTldrAndBody()
    {
        var (tldr, body) = DossierSummaryService.SplitSections("## TL;DR\nKurz.\n\n## Zusammenfassung\nLang.\nMehr.");
        Assert.Equal("Kurz.", tldr);
        Assert.Contains("Lang.", body);
        Assert.DoesNotContain("TL;DR", body);
    }

    [Fact]
    public void Split_NoHeadings_AllBody()
    {
        var (tldr, body) = DossierSummaryService.SplitSections("Nur Fließtext ohne Struktur.");
        Assert.Null(tldr);
        Assert.Equal("Nur Fließtext ohne Struktur.", body);
    }

    [Fact]
    public void Markdown_RendersFormatting()
    {
        var html = MarkdownRenderer.ToSafeHtml("**fett**\n\n- eins\n- zwei");
        Assert.Contains("<strong>fett</strong>", html);
        Assert.Contains("<li>", html);
    }

    [Fact]
    public void Markdown_DropsRawHtml()
    {
        var html = MarkdownRenderer.ToSafeHtml("Text <script>alert(1)</script> mehr");
        Assert.DoesNotContain("<script", html);
    }
}
