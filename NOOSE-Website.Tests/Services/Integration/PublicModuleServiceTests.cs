using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Infrastructure;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Services;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Guard tests for <see cref="PublicModuleService"/>: who may flip a switch, and what a switch actually does.</summary>
public sealed class PublicModuleServiceTests
{
    private static ClaimsPrincipal Admin()
        => ClaimsPrincipalBuilder.Agent("admin").WithRank(Rank.Director).AsAdmin().WithCodename("Falcon").Build();

    private static ClaimsPrincipal PlainAgent()
        => ClaimsPrincipalBuilder.Agent("agent-1").WithRank(Rank.SpecialAgent).Build();

    private static ClaimsPrincipal Leader()
        => ClaimsPrincipalBuilder.Agent("lead").WithRank(Rank.Director).Build();

    /// <summary>Read-only supervision: leadership rank, no admin flag, team-lead marker.</summary>
    private static ClaimsPrincipal OnlyReader()
        => ClaimsPrincipalBuilder.Agent("aufsicht").WithRank(Rank.Director).AsTeamLead().Build();

    private static PublicModuleService NewService(SqliteTestContext ctx)
        => new(ctx.Factory, new MemoryCache(new MemoryCacheOptions()));

    private static async Task<SqliteTestContext> SeededAsync()
    {
        var ctx = new SqliteTestContext();
        await using var db = ctx.NewContext();
        await PublicModuleSeeder.SeedAsync(db);
        return ctx;
    }

    // ---- seeding ----

    [Fact]
    public async Task Seeder_CreatesOneRowPerCatalogModule()
    {
        using var ctx = await SeededAsync();

        await using var db = ctx.NewContext();
        var keys = await db.OeffentlicheModule.Select(m => m.Key).ToListAsync();
        Assert.Equal(PublicModules.All.Count, keys.Count);
        Assert.All(PublicModules.All, definition => Assert.Contains(definition.Key, keys));
    }

    [Fact]
    public async Task Seeder_IsIdempotent()
    {
        using var ctx = await SeededAsync();

        await using (var db = ctx.NewContext())
        {
            await PublicModuleSeeder.SeedAsync(db);
        }

        await using var check = ctx.NewContext();
        Assert.Equal(PublicModules.All.Count, await check.OeffentlicheModule.CountAsync());
    }

    [Fact]
    public async Task Seeder_NeverOverwritesAStoredChoice()
    {
        // a later phase that ships a module must not switch it on behind the operator's back
        using var ctx = await SeededAsync();
        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Careers);
            row.IsEnabled = false;
            await db.SaveChangesAsync();
        }

        await using (var db = ctx.NewContext())
        {
            await PublicModuleSeeder.SeedAsync(db);
        }

        await using var check = ctx.NewContext();
        Assert.False((await check.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Careers)).IsEnabled);
    }

    // ---- reading ----

    [Fact]
    public async Task GetAsync_WithoutRows_FallsBackToCatalogDefaults()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        var snapshot = await service.GetAsync();

        Assert.Equal(PublicModules.All.Count, snapshot.Modules.Count);
        Assert.True(snapshot.IsEnabled(PublicModules.Careers));
        Assert.False(snapshot.IsEnabled(PublicModules.Wanted));
    }

    [Fact]
    public async Task IsEnabledAsync_UnknownKey_IsNeverEnabled()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        Assert.False(await service.IsEnabledAsync("GibtsNicht"));
    }

    [Fact]
    public async Task GetAsync_IsCached_SoADirectRowEditIsNotSeenImmediately()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        Assert.True(await service.IsEnabledAsync(PublicModules.Careers));

        await using (var db = ctx.NewContext())
        {
            var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Careers);
            row.IsEnabled = false;
            await db.SaveChangesAsync();
        }

        Assert.True(await service.IsEnabledAsync(PublicModules.Careers));
    }

    [Fact]
    public async Task SaveAsync_DropsTheCache_SoTheNextReadIsFresh()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        Assert.True(await service.IsEnabledAsync(PublicModules.Careers));

        await service.SaveAsync([Input(PublicModules.Careers, enabled: false)], Admin());

        Assert.False(await service.IsEnabledAsync(PublicModules.Careers));
    }

    [Fact]
    public async Task OfflineTextAsync_FallsBackToTheCatalogText()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var text = await service.OfflineTextAsync(PublicModules.Careers);

        Assert.Equal(PublicModules.Find(PublicModules.Careers)!.DefaultOfflineText, text);
    }

    [Fact]
    public async Task OfflineTextAsync_PrefersTheStoredText()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync([Input(PublicModules.Careers, enabled: false, offline: "Wir pausieren.")], Admin());

        Assert.Equal("Wir pausieren.", await service.OfflineTextAsync(PublicModules.Careers));
    }

    [Fact]
    public async Task OfflineTextAsync_UnknownKey_StillAnswers()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        Assert.False(string.IsNullOrWhiteSpace(await service.OfflineTextAsync("GibtsNicht")));
    }

    // ---- require ----

    [Fact]
    public async Task RequireEnabledAsync_Throws_WhenTheModuleIsOff()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequireEnabledAsync(PublicModules.Wanted));
    }

    [Fact]
    public async Task RequireEnabledAsync_Passes_WhenTheModuleIsOn()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.RequireEnabledAsync(PublicModules.Careers);
    }

    [Fact]
    public async Task RequireEnabledAsync_UnknownKey_Throws()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequireEnabledAsync("GibtsNicht"));
    }

    // ---- kill switch ----

    [Fact]
    public async Task KillSwitch_BeatsEveryIndividualSwitch()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        Assert.True(await service.IsEnabledAsync(PublicModules.Careers));

        await service.KillSwitchSetAsync(true, Admin());

        var snapshot = await service.GetAsync();
        Assert.True(snapshot.KillSwitchActive);
        Assert.False(snapshot.IsEnabled(PublicModules.Careers));
        Assert.Empty(snapshot.NavEntries());
    }

    [Fact]
    public async Task KillSwitch_LeavesTheStoredChoicesUntouched()
    {
        // the switch shuts the door, it does not rewrite anyone's settings — otherwise turning it off again
        // would silently publish a different public area than the one that was there before
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.KillSwitchSetAsync(true, Admin());
        var during = await service.GetAsync();
        Assert.True(during.Find(PublicModules.Careers)!.IsEnabled);

        await service.KillSwitchSetAsync(false, Admin());
        Assert.True(await service.IsEnabledAsync(PublicModules.Careers));
    }

    [Fact]
    public async Task KillSwitch_RequireEnabled_NamesTheWholeArea()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        await service.KillSwitchSetAsync(true, Admin());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequireEnabledAsync(PublicModules.Careers));

        Assert.Contains("öffentliche Bereich", error.Message);
    }

    [Fact]
    public async Task KillSwitch_WritesAnAuditRow()
    {
        // SystemSetting carries no audit stamps, so without this row the shutdown would leave no trace
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.KillSwitchSetAsync(true, Admin());

        await using var db = ctx.NewContext();
        var row = await db.AuditLogs.SingleAsync(a => a.EntityType == PublicModuleService.AuditType);
        Assert.Equal(AuditAction.Modified, row.Action);
        Assert.Equal("admin", row.AgentId);
        Assert.NotNull(row.ChangesJson);
    }

    [Fact]
    public async Task KillSwitch_IsAdminOnly()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.KillSwitchSetAsync(true, Leader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.KillSwitchSetAsync(true, OnlyReader()));
    }

    // ---- writing ----

    [Fact]
    public async Task SaveAsync_IsAdminOnly()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var input = new[] { Input(PublicModules.Careers, enabled: false) };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(input, PlainAgent()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(input, Leader()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(input, OnlyReader()));
    }

    [Fact]
    public async Task SaveAsync_UnknownKey_Throws()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync([Input("GibtsNicht", enabled: true)], Admin()));
    }

    [Fact]
    public async Task SaveAsync_ARepeatedKeyWinsInsteadOfHittingTheUniqueIndex()
    {
        // a caller that merges two lists must not blow up on the unique index
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await service.SaveAsync(
            [Input(PublicModules.Wanted, enabled: false), Input(PublicModules.Wanted, enabled: true)],
            Admin());

        await using var db = ctx.NewContext();
        var rows = await db.OeffentlicheModule.Where(m => m.Key == PublicModules.Wanted).ToListAsync();
        Assert.Single(rows);
        Assert.True(rows[0].IsEnabled);
    }

    [Fact]
    public async Task SaveAsync_CreatesAMissingRow()
    {
        using var ctx = new SqliteTestContext();
        var service = NewService(ctx);

        await service.SaveAsync([Input(PublicModules.Wanted, enabled: true)], Admin());

        await using var db = ctx.NewContext();
        Assert.True((await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Wanted)).IsEnabled);
    }

    [Fact]
    public async Task SaveAsync_RejectsAnIconThatIsNotOnTheAllowlist()
    {
        // MudBlazor renders an icon value as markup, so a free-text icon would run for every anonymous visitor
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync(
            [new PublicModuleInput { Key = PublicModules.Careers, IsEnabled = true, IconOverride = "<script>x</script>" }],
            Admin());

        await using var db = ctx.NewContext();
        Assert.Null((await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Careers)).IconOverride);
    }

    [Fact]
    public async Task SaveAsync_KeepsAnIconFromTheAllowlist()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);
        var choice = PublicModules.IconChoices[0];

        await service.SaveAsync(
            [new PublicModuleInput { Key = PublicModules.Careers, IsEnabled = true, IconOverride = choice.Name }],
            Admin());

        var state = (await service.GetAsync()).Find(PublicModules.Careers)!;
        Assert.Equal(choice.Name, state.IconOverride);
        Assert.Equal(choice.Icon, state.Icon);
    }

    [Fact]
    public async Task SaveAsync_ClampsSortOrderAndCutsAnOverlongLabel()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync(
            [new PublicModuleInput
            {
                Key = PublicModules.Careers,
                IsEnabled = true,
                SortOrder = -5,
                LabelOverride = new string('x', 200),
            }],
            Admin());

        await using var db = ctx.NewContext();
        var row = await db.OeffentlicheModule.SingleAsync(m => m.Key == PublicModules.Careers);
        Assert.Equal(0, row.SortOrder);
        Assert.Equal(64, row.LabelOverride!.Length);
    }

    [Fact]
    public async Task SaveAsync_BlankOverridesBecomeNull_NotEmptyStrings()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync(
            [new PublicModuleInput
            {
                Key = PublicModules.Careers,
                IsEnabled = true,
                LabelOverride = "   ",
                OfflineText = "  ",
            }],
            Admin());

        var state = (await service.GetAsync()).Find(PublicModules.Careers)!;
        Assert.Null(state.LabelOverride);
        Assert.Null(state.OfflineTextOverride);
        Assert.Equal(PublicModules.Find(PublicModules.Careers)!.Label, state.Label);
    }

    [Fact]
    public async Task SaveAsync_AnOverriddenLabelReachesTheNav()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync(
            [new PublicModuleInput { Key = PublicModules.Careers, IsEnabled = true, LabelOverride = "Mitarbeit" }],
            Admin());

        var entries = await service.NavEntriesAsync();
        Assert.Contains(entries, e => e.Label == "Mitarbeit" && e.NavRoute == "/karriere");
    }

    // ---- nav ----

    [Fact]
    public async Task NavEntries_ContainOnlyEnabledModulesWithARoute()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        var entries = await service.NavEntriesAsync();

        Assert.All(entries, e => Assert.True(e.IsEnabled));
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.NavRoute)));
        Assert.DoesNotContain(entries, e => e.Key == PublicModules.Wanted);
    }

    [Fact]
    public async Task NavEntries_ExcludeAModuleWhosePagesDoNotExistYet()
    {
        // pre-configuring an unbuilt module is allowed; a tab pointing at a 404 is not
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync([Input(PublicModules.Wanted, enabled: true)], Admin());

        var entries = await service.NavEntriesAsync();
        Assert.DoesNotContain(entries, e => e.Key == PublicModules.Wanted);
        // the choice itself is stored, so the tab appears by itself once the pages ship
        Assert.True((await service.GetAsync()).Find(PublicModules.Wanted)!.IsEnabled);
    }

    [Fact]
    public async Task NavEntries_ExcludeAModuleWithoutARoute_EvenWhenEnabled()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.SaveAsync([Input(PublicModules.Bounty, enabled: true)], Admin());

        Assert.DoesNotContain(await service.NavEntriesAsync(), e => e.Key == PublicModules.Bounty);
    }

    // ---- helpers ----

    private static PublicModuleInput Input(string key, bool enabled, string? offline = null)
        => new()
        {
            Key = key,
            IsEnabled = enabled,
            OfflineText = offline,
            SortOrder = PublicModules.Find(key)?.SortOrder ?? 0,
        };

    [Fact]
    public async Task KillSwitchKey_IsStoredAsASystemSetting()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.KillSwitchSetAsync(true, Admin());

        await using var db = ctx.NewContext();
        var row = await db.SystemSettings.SingleAsync(s => s.Key == SystemSettingKeys.PublicAreaKillSwitch);
        Assert.Equal("true", row.Value);
    }

    [Fact]
    public async Task KillSwitch_SecondCallUpdatesTheExistingRow()
    {
        using var ctx = await SeededAsync();
        var service = NewService(ctx);

        await service.KillSwitchSetAsync(true, Admin());
        await service.KillSwitchSetAsync(false, Admin());

        await using var db = ctx.NewContext();
        var rows = await db.SystemSettings.Where(s => s.Key == SystemSettingKeys.PublicAreaKillSwitch).ToListAsync();
        Assert.Single(rows);
        Assert.Equal("false", rows[0].Value);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.EntityType == PublicModuleService.AuditType));
    }

    /// <summary>Stub acting agent for the interceptor-backed audit test.</summary>
    private sealed class FixedUser : ICurrentUserService
    {
        public Task<CurrentUserInfo> GetAsync() => Task.FromResult(Get());

        public CurrentUserInfo Get() => new("admin", "Falcon", true, false, false);
    }

    [Fact]
    public async Task KillSwitch_LeavesTwoTraces_TheNamedActionAndTheRawSetting()
    {
        // SystemSetting is IAuditable, so the interceptor logs the key/value change on its own. The manual row is
        // there because "SystemSetting/OeffentlicherBereichNotAus" is not something a reader can act on — pinning
        // both down here so nobody later removes the readable one believing it is a duplicate.
        using var ctx = new SqliteTestContext();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        var service = new PublicModuleService(new TestDbContextFactory(options), new MemoryCache(new MemoryCacheOptions()));

        await service.KillSwitchSetAsync(true, Admin());

        await using var read = ctx.NewContext();
        Assert.Equal(1, await read.AuditLogs.CountAsync(a => a.EntityType == PublicModuleService.AuditType));
        Assert.Equal(1, await read.AuditLogs.CountAsync(a => a.EntityType == nameof(SystemSetting)));
        // both are readable in /nachweis rather than showing a raw CLR name
        Assert.Equal("Öffentlicher Bereich", AuditEntityDisplay.Label(PublicModuleService.AuditType));
        Assert.Equal("Systemeinstellung", AuditEntityDisplay.Label(nameof(SystemSetting)));
    }

    [Fact]
    public async Task AFlippedSwitch_IsAuditedByTheInterceptor_WithoutAManualRow()
    {
        using var ctx = new SqliteTestContext();
        // the shared context omits the interceptors on purpose; OeffentlichesModul is IAuditable, so wiring the
        // real one up is what proves a flip is logged without ManualAudit anywhere in SaveAsync
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(ctx.Connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FixedUser()))
            .Options;
        var service = new PublicModuleService(new TestDbContextFactory(options), new MemoryCache(new MemoryCacheOptions()));

        await service.SaveAsync([Input(PublicModules.Wanted, enabled: true)], Admin());
        await service.SaveAsync([Input(PublicModules.Wanted, enabled: false)], Admin());

        await using var read = ctx.NewContext();
        var rows = await read.AuditLogs
            .Where(a => a.EntityType == nameof(OeffentlichesModul))
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(AuditAction.Created, rows[0].Action);
        Assert.Equal(AuditAction.Modified, rows[1].Action);
    }
}
