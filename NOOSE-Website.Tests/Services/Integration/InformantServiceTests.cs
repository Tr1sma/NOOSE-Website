using System.Security.Claims;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Informants;
using NOOSE_Website.Services;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Security-critical tests for informant record visibility plus person and faction linking.</summary>
public sealed class InformantServiceTests
{
    private static ClaimsPrincipal Leader() => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();
    private static ClaimsPrincipal Handler(string id) => ClaimsPrincipalBuilder.Agent(id).WithRank(Rank.SpecialAgent).Build();
    private static ClaimsPrincipal Stranger() => ClaimsPrincipalBuilder.Agent("stranger").WithRank(Rank.SpecialAgent).Build();
    private static ClaimsPrincipal OnlyReader() => ClaimsPrincipalBuilder.Agent("or").AsTeamLead().Build();
    private static ClaimsPrincipal Junior() => ClaimsPrincipalBuilder.Agent("j").WithRank(Rank.JuniorAgent).Build();
    private static ClaimsPrincipal Partner() => ClaimsPrincipalBuilder.Agent("p")
        .AsPartner(PartnerAgency.DoJ, PartnerRank.Chief).Build();

    // shared across every service instance in one test: case numbers are unique-indexed
    private int _caseNumber;

    private InformantService Svc(SqliteTestContext ctx)
    {
        var caseNumbers = Substitute.For<ICaseNumberService>();
        caseNumbers.NextAsync(Arg.Any<AppDbContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult($"NOOSE-VP-2026-{++_caseNumber:0000}"));
        return new InformantService(ctx.Factory, caseNumbers);
    }

    private static InformantInput NewInput(string handlerId = "handler1", string? realName = "Max Mustermann",
        string? personId = null, string? factionId = null)
        => new(realName, personId, factionId, "Kontakt im Hafen", InformantReliability.B, InformantStatus.Active,
            handlerId, "0900-123", "Vorsicht");

    private async Task<string> SeedAsync(SqliteTestContext ctx, string handlerId = "handler1")
        => await Svc(ctx).CreateAsync(NewInput(handlerId), Leader());

    // Seeds a person record and returns its id.
    private static string SeedPerson(SqliteTestContext ctx, string name = "Klara Klarname", bool classified = false)
    {
        using var db = ctx.NewContext();
        var person = Seed.Person(name: name, configure: p => p.IsClassified = classified);
        db.People.Add(person);
        db.SaveChanges();
        return person.Id;
    }

    // Seeds a faction record and returns its id.
    private static string SeedFaction(SqliteTestContext ctx, string name = "Ballas")
    {
        using var db = ctx.NewContext();
        var faction = Seed.Faction(name: name);
        db.Factions.Add(faction);
        db.SaveChanges();
        return faction.Id;
    }

    // ==================== record visibility ====================

    [Fact]
    public async Task Handler_SeesOwnInformant_WithContactData()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx, "handler1");
        var svc = Svc(ctx);

        Assert.Contains(await svc.GetListAsync(Handler("handler1")), i => i.Id == id);
        var detail = await svc.GetDetailAsync(id, Handler("handler1"));
        Assert.NotNull(detail);
        Assert.Equal("Max Mustermann", detail!.Name);
        Assert.Equal("0900-123", detail.ContactInfo);
    }

    [Fact]
    public async Task Leadership_SeesContactData()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx);

        var detail = await Svc(ctx).GetDetailAsync(id, Leader());
        Assert.NotNull(detail);
        Assert.Equal("Max Mustermann", detail!.Name);
        Assert.Equal("0900-123", detail.ContactInfo);
    }

    [Fact]
    public async Task OnlyReader_SeesTheWholeRecord_IncludingContactData()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx);
        var svc = Svc(ctx);

        // record access is all-or-nothing: no second tier hides the name or the contact channel
        Assert.Contains(await svc.GetListAsync(OnlyReader()), i => i.Id == id);
        var detail = await svc.GetDetailAsync(id, OnlyReader());
        Assert.NotNull(detail);
        Assert.Equal("Max Mustermann", detail!.Name);
        Assert.Equal("0900-123", detail.ContactInfo);
        Assert.Equal("Vorsicht", detail.Notes);
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

    // ==================== name source ====================

    [Fact]
    public async Task LinkedPerson_ProvidesTheName_AndClearsFreeText()
    {
        using var ctx = new SqliteTestContext();
        var personId = SeedPerson(ctx, "Klara Klarname");
        var svc = Svc(ctx);

        var id = await svc.CreateAsync(NewInput(realName: "Wird ignoriert", personId: personId), Leader());

        var detail = await svc.GetDetailAsync(id, Leader());
        Assert.Equal("Klara Klarname", detail!.Name);
        Assert.Equal(personId, detail.PersonId);

        using var db = ctx.NewContext();
        Assert.Null(db.Informants.Single(i => i.Id == id).RealName);
    }

    [Fact]
    public async Task RenamingLinkedPerson_RenamesTheInformant()
    {
        using var ctx = new SqliteTestContext();
        var personId = SeedPerson(ctx, "Alter Name");
        var svc = Svc(ctx);
        var id = await svc.CreateAsync(NewInput(realName: null, personId: personId), Leader());

        using (var db = ctx.NewContext())
        {
            db.People.Single(p => p.Id == personId).Name = "Neuer Name";
            db.SaveChanges();
        }

        Assert.Equal("Neuer Name", (await svc.GetDetailAsync(id, Leader()))!.Name);
    }

    [Fact]
    public async Task Unlinking_CarriesThePersonNameOver()
    {
        using var ctx = new SqliteTestContext();
        var personId = SeedPerson(ctx, "Klara Klarname");
        var svc = Svc(ctx);
        var id = await svc.CreateAsync(NewInput(realName: null, personId: personId), Leader());

        await svc.UpdateAsync(id, NewInput(realName: null, personId: null), Leader());

        var detail = await svc.GetDetailAsync(id, Leader());
        Assert.Null(detail!.PersonId);
        Assert.Equal("Klara Klarname", detail.Name);
    }

    [Fact]
    public async Task Create_Throws_WithoutNameOrPerson()
    {
        using var ctx = new SqliteTestContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Svc(ctx).CreateAsync(NewInput(realName: null), Leader()));
    }

    [Fact]
    public async Task Create_Throws_WhenPersonAlreadyLinked()
    {
        using var ctx = new SqliteTestContext();
        var personId = SeedPerson(ctx);
        var svc = Svc(ctx);
        await svc.CreateAsync(NewInput(realName: null, personId: personId), Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreateAsync(NewInput(realName: null, personId: personId), Leader()));
    }

    [Fact]
    public async Task Create_Throws_ForUnknownPerson()
    {
        using var ctx = new SqliteTestContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Svc(ctx).CreateAsync(NewInput(realName: null, personId: "does-not-exist"), Leader()));
    }

    // ==================== person marker ====================

    [Fact]
    public async Task PersonMarker_IsVisibleToEveryInternalAgent_ButOnlyOpenableByTheTier()
    {
        using var ctx = new SqliteTestContext();
        var personId = SeedPerson(ctx);
        var svc = Svc(ctx);
        var id = await svc.CreateAsync(NewInput("handler1", realName: null, personId: personId), Leader());

        var forStranger = await svc.GetPersonMarkerAsync(personId, Stranger());
        Assert.NotNull(forStranger);
        Assert.Equal(id, forStranger!.InformantId);
        Assert.False(forStranger.MayOpen);

        Assert.True((await svc.GetPersonMarkerAsync(personId, Handler("handler1")))!.MayOpen);
        Assert.True((await svc.GetPersonMarkerAsync(personId, Leader()))!.MayOpen);
    }

    [Fact]
    public async Task PersonMarker_IsNullForPartnersAndUnlinkedPeople()
    {
        using var ctx = new SqliteTestContext();
        var personId = SeedPerson(ctx);
        var otherPersonId = SeedPerson(ctx, "Ohne Informant");
        var svc = Svc(ctx);
        await svc.CreateAsync(NewInput(realName: null, personId: personId), Leader());

        Assert.Null(await svc.GetPersonMarkerAsync(personId, Partner()));
        Assert.Null(await svc.GetPersonMarkerAsync(otherPersonId, Leader()));
    }

    // ==================== faction link ====================

    [Fact]
    public async Task FactionLink_IsResolvedForDisplay()
    {
        using var ctx = new SqliteTestContext();
        var factionId = SeedFaction(ctx, "Ballas");
        var svc = Svc(ctx);

        var id = await svc.CreateAsync(NewInput(factionId: factionId), Leader());

        var detail = await svc.GetDetailAsync(id, Leader());
        Assert.Equal(factionId, detail!.FactionId);
        Assert.Equal("Ballas", detail.FactionName);
    }

    [Fact]
    public async Task FactionLink_IsNotExclusive_SeveralInformantsPerFaction()
    {
        using var ctx = new SqliteTestContext();
        var factionId = SeedFaction(ctx);
        var svc = Svc(ctx);
        await svc.CreateAsync(NewInput(realName: "Erster", factionId: factionId), Leader());
        await svc.CreateAsync(NewInput(realName: "Zweiter", factionId: factionId), Leader());

        var entries = await svc.GetForFactionAsync(factionId, Leader());
        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { "Erster", "Zweiter" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.True(e.MayOpen));
    }

    [Fact]
    public async Task FactionRoster_StaysAnonymousWithoutRecordAccess()
    {
        using var ctx = new SqliteTestContext();
        var factionId = SeedFaction(ctx);
        var svc = Svc(ctx);
        await svc.CreateAsync(NewInput("handler1", factionId: factionId), Leader());

        var entry = Assert.Single(await svc.GetForFactionAsync(factionId, Stranger()));
        Assert.False(entry.MayOpen);
        Assert.Equal(entry.CaseNumber, entry.Name);
        Assert.DoesNotContain("Mustermann", entry.Name);
    }

    [Fact]
    public async Task FactionRoster_IsEmptyForPartners()
    {
        using var ctx = new SqliteTestContext();
        var factionId = SeedFaction(ctx);
        var svc = Svc(ctx);
        await svc.CreateAsync(NewInput(factionId: factionId), Leader());

        Assert.Empty(await svc.GetForFactionAsync(factionId, Partner()));
    }

    [Fact]
    public async Task Create_Throws_ForUnknownFaction()
    {
        using var ctx = new SqliteTestContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Svc(ctx).CreateAsync(NewInput(factionId: "does-not-exist"), Leader()));
    }

    [Fact]
    public async Task FactionLink_CanBeCleared()
    {
        using var ctx = new SqliteTestContext();
        var factionId = SeedFaction(ctx);
        var svc = Svc(ctx);
        var id = await svc.CreateAsync(NewInput(factionId: factionId), Leader());

        await svc.UpdateAsync(id, NewInput(factionId: null), Leader());

        Assert.Null((await svc.GetDetailAsync(id, Leader()))!.FactionId);
        Assert.Empty(await svc.GetForFactionAsync(factionId, Leader()));
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

    [Fact]
    public async Task Update_WritesEveryField()
    {
        using var ctx = new SqliteTestContext();
        var id = await SeedAsync(ctx, "handler1");
        var svc = Svc(ctx);

        await svc.UpdateAsync(id, new InformantInput("Neuer Name", null, null, "Neu", InformantReliability.A,
            InformantStatus.Inactive, "handler1", "0700-999", "Neue Notiz"), Leader());

        var detail = await svc.GetDetailAsync(id, Handler("handler1"));
        Assert.Equal("Neuer Name", detail!.Name);
        Assert.Equal("0700-999", detail.ContactInfo);
        Assert.Equal("Neue Notiz", detail.Notes);
        Assert.Equal(InformantStatus.Inactive, detail.Status);
    }
}
