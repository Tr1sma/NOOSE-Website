using System.Security.Claims;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Informants;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Security-critical tests for informant two-tier secrecy (codename vs. real identity).</summary>
public sealed class InformantServiceTests
{
    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
    private static ClaimsPrincipal Handler(string id) => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SpecialAgent).Build();
    private static ClaimsPrincipal Stranger() => ClaimsPrincipalBuilder.Agent("stranger").WithRank(Rank.SpecialAgent).Build();
    private static ClaimsPrincipal OnlyReader() => ClaimsPrincipalBuilder.Agent("or").AsTeamLead().Build();
    private static ClaimsPrincipal Junior() => ClaimsPrincipalBuilder.Agent("j").WithRank(Rank.JuniorAgent).Build();

    private static InformantService Svc(SqliteTestContext ctx)
    {
        var caseNumbers = Substitute.For<ICaseNumberService>();
        caseNumbers.NextAsync(Arg.Any<AppDbContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("NOOSE-VP-2026-0001");
        return new InformantService(ctx.Factory, caseNumbers);
    }

    private static InformantInput NewInput(string handlerId = "handler1", string? realName = "Max Mustermann")
        => new("Falke", "Kontakt im Hafen", InformantReliability.B, InformantStatus.Active, handlerId, realName, "0900-123", "Vorsicht");

    private static async Task<string> SeedAsync(SqliteTestContext ctx, string handlerId = "handler1")
        => await Svc(ctx).CreateAsync(NewInput(handlerId), Leader());

    // ==================== identity tier ====================

    [Fact]
    public async Task Handler_SeesOwnInformant_WithRealIdentity()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx, "handler1");
        var svc = Svc(ctx);

        Assert.Contains(await svc.GetListAsync(Handler("handler1")), i => i.Id == id);
        var detail = await svc.GetDetailAsync(id, Handler("handler1"));
        Assert.NotNull(detail);
        Assert.True(detail!.MaySeeIdentity);
        Assert.Equal("Max Mustermann", detail.RealName);
    }

    [Fact]
    public async Task Leadership_SeesRealIdentity()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx);

        var detail = await Svc(ctx).GetDetailAsync(id, Leader());
        Assert.NotNull(detail);
        Assert.Equal("Max Mustermann", detail!.RealName);
    }

    [Fact]
    public async Task OnlyReader_SeesCodename_ButNeverIdentity()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx);
        var svc = Svc(ctx);

        // codename tier: the record is listed
        Assert.Contains(await svc.GetListAsync(OnlyReader()), i => i.Id == id);
        var detail = await svc.GetDetailAsync(id, OnlyReader());
        Assert.NotNull(detail);
        Assert.Equal("Falke", detail!.Codename);
        // identity tier: never
        Assert.False(detail.MaySeeIdentity);
        Assert.Null(detail.RealName);
        Assert.Null(detail.ContactInfo);
    }

    [Fact]
    public async Task Stranger_SeesNothing()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx, "handler1");
        var svc = Svc(ctx);

        Assert.Empty(await svc.GetListAsync(Stranger()));
        Assert.Null(await svc.GetDetailAsync(id, Stranger()));
    }

    [Fact]
    public async Task Handler_DoesNotSeeOtherHandlersInformant()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx, "handler1");
        var svc = Svc(ctx);

        Assert.Empty(await svc.GetListAsync(Handler("handler2")));
        Assert.Null(await svc.GetDetailAsync(id, Handler("handler2")));
    }

    // ==================== write guards ====================

    [Fact]
    public async Task Create_Throws_ForNonLeadership()
    {
        using var ctx = new SqliteTestContext();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Svc(ctx).CreateAsync(NewInput(), Junior()));
    }

    [Fact]
    public async Task AddMeeting_AllowedForHandler_DeniedForStranger()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx, "handler1");
        var svc = Svc(ctx);
        var meeting = new InformantMeetingInput(DateTime.UtcNow, "Hafen", "Übergabe beobachtet");

        await svc.AddMeetingAsync(id, meeting, Handler("handler1")); // ok
        Assert.Single(await svc.GetMeetingsAsync(id, Handler("handler1")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.AddMeetingAsync(id, meeting, Stranger()));
    }
}
