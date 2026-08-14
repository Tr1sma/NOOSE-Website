using System.Security.Claims;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>The editorial value list behind the public warning chips.</summary>
/// <remarks>
/// The label reaches an anonymous page without ever passing the publication check, so the write path has to hold the
/// same three content rules as an accusation. That is what most of these facts are about.
/// </remarks>
public sealed class WarnhinweisServiceTests
{
    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).WithCodename("Falcon").Build();

    private static ClaimsPrincipal PlainAgent()
        => ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.SpecialAgent).Build();

    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static WarnhinweisService NewService(SqliteTestContext ctx)
        => new(new TestDbContextFactory(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection).Options));

    // ---- guards ----

    [Fact]
    public async Task Creating_AWarning_RequiresLeadership()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync("bewaffnet", "Error", 10, true, PlainAgent()));
    }

    [Fact]
    public async Task Creating_AWarning_IsRefusedForTheReadOnlySupervision()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync("bewaffnet", "Error", 10, true, OnlyReader()));
    }

    // ---- content rules ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyLabel_IsRefused(string name)
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(name, "Error", 10, true, Leader()));
    }

    [Theory]
    [InlineData("<b>bewaffnet</b>")]
    [InlineData("Vorsicht bei @{Person:11111111-1111-1111-1111-111111111111}")]
    [InlineData("Achtung {{Name}}")]
    public async Task ALabelThatIsNotPlainText_IsRefused(string name)
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(name, "Error", 10, true, Leader()));
    }

    [Fact]
    public async Task ALabelOverSixtyCharacters_IsRefused()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(new string('x', 61), "Error", 10, true, Leader()));
    }

    [Fact]
    public async Task ADuplicateLabel_IsRefused()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync("bewaffnet", "Warning", 20, true, Leader()));
    }

    [Fact]
    public async Task RenamingToAnExistingLabel_IsRefused()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());
        var second = await service.CreateAsync("gewaltbereit", "Error", 20, true, Leader());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefreshAsync(second.Id, "bewaffnet", "Error", 20, true, Leader()));
    }

    [Fact]
    public async Task RenamingAWarningToItsOwnLabel_IsAllowed()
    {
        // the duplicate check spells out the two branches: `Id != null` translates to SQL NULL and matches nothing
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        var row = await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());

        await service.RefreshAsync(row.Id, "bewaffnet", "Warning", 30, false, Leader());

        var stored = (await service.GetAllAsync()).Single();
        Assert.Equal("Warning", stored.Colour);
        Assert.Equal(30, stored.SortOrder);
        Assert.False(stored.IsActive);
    }

    // ---- colour allowlist ----

    [Theory]
    [InlineData("Rot")]
    [InlineData("Inherit")]
    [InlineData("'; DROP TABLE Warnhinweise; --")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AColourOutsideTheAllowlist_IsStoredAsNull(string? colour)
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        var row = await service.CreateAsync("bewaffnet", colour, 10, true, Leader());

        Assert.Null(row.Colour);
    }

    [Fact]
    public void ResolvingAnUnknownColour_FallsBackInsteadOfThrowing()
    {
        // Enum.Parse here would be an HTTP 500 on an [AllowAnonymous] page
        Assert.Equal(MudBlazor.Color.Default, WarnhinweisColours.Resolve("Rot"));
        Assert.Equal(MudBlazor.Color.Default, WarnhinweisColours.Resolve(null));
        Assert.Equal(MudBlazor.Color.Error, WarnhinweisColours.Resolve("Error"));
    }

    [Fact]
    public void EveryOfferedColour_IsVisibleOnTheDarkPublicBackground()
    {
        string[] invisible = ["Inherit", "Transparent", "Surface", "Dark"];

        Assert.All(WarnhinweisColours.All, c => Assert.DoesNotContain(c.Name, invisible));
    }

    // ---- reads ----

    [Fact]
    public async Task OnlyActiveWarnings_AreOfferedToThePicker()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());
        await service.CreateAsync("alt", "Info", 20, false, Leader());

        var offered = await service.GetActiveAsync();

        Assert.Single(offered);
        Assert.Equal("bewaffnet", offered[0].Name);
    }

    [Fact]
    public async Task Deleting_AWarning_TakesItsAssignmentsWithIt()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        var row = await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());
        await using (var db = ctx.NewContext())
        {
            db.OeffentlicheFahndungen.Add(new OeffentlicheFahndung { Id = "f1", DisplayName = "Max" });
            db.FahndungWarnhinweise.Add(new FahndungWarnhinweis { FahndungId = "f1", WarnhinweisId = row.Id });
            await db.SaveChangesAsync();
        }

        await service.DeleteAsync(row.Id, Leader());

        await using var check = ctx.NewContext();
        Assert.Empty(check.Warnhinweise);
        Assert.Empty(check.FahndungWarnhinweise);
        // and the name is free again — which is why this list is hard-deleted, like Tag
        await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());
    }

    [Fact]
    public async Task Usage_CountsTheAssignments()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        var row = await service.CreateAsync("bewaffnet", "Error", 10, true, Leader());
        await using (var db = ctx.NewContext())
        {
            db.OeffentlicheFahndungen.Add(new OeffentlicheFahndung { Id = "f1", DisplayName = "Max" });
            db.OeffentlicheFahndungen.Add(new OeffentlicheFahndung { Id = "f2", DisplayName = "Moritz" });
            db.FahndungWarnhinweise.Add(new FahndungWarnhinweis { FahndungId = "f1", WarnhinweisId = row.Id });
            db.FahndungWarnhinweise.Add(new FahndungWarnhinweis { FahndungId = "f2", WarnhinweisId = row.Id });
            await db.SaveChangesAsync();
        }

        var usage = await service.GetWithUsageAsync();

        Assert.Equal(2, usage.Single().Count);
    }

    // ---- seeder ----

    [Fact]
    public async Task TheSeeder_AddsTheFourStartingValuesOnce()
    {
        using var ctx = new SqliteTestContext();
        await using (var db = ctx.NewContext())
        {
            await WarnhinweisSeeder.SeedAsync(db);
            await WarnhinweisSeeder.SeedAsync(db);
        }

        await using var check = ctx.NewContext();
        Assert.Equal(4, check.Warnhinweise.Count());
    }

    [Fact]
    public async Task TheSeeder_DoesNotResurrectADeletedValue()
    {
        // deliberately unlike PublicModuleSeeder, which tops up per key: a module key lives in the code, a warning
        // is a row that belongs to whoever runs the site
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);
        await using (var db = ctx.NewContext())
        {
            await WarnhinweisSeeder.SeedAsync(db);
        }
        var doomed = (await service.GetAllAsync()).Single(w => w.Name == "gewaltbereit");
        await service.DeleteAsync(doomed.Id, Leader());

        await using (var db = ctx.NewContext())
        {
            await WarnhinweisSeeder.SeedAsync(db);
        }

        var names = (await service.GetAllAsync()).Select(w => w.Name).ToArray();
        Assert.DoesNotContain("gewaltbereit", names);
        Assert.Equal(3, names.Length);
    }
}
