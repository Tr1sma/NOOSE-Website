using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="SystemSettingService"/> over in-memory SQLite plus a real temp content root for the logo file paths.</summary>
public sealed class SystemSettingServiceTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    // --- construction / collaborator setup ---

    private (SystemSettingService Svc, string ContentRoot, IMemoryCache Cache) Build(
        SqliteTestContext ctx, FileUploadOptions? options = null)
    {
        var contentRoot = Path.Combine(
            Path.GetTempPath(), "noose-systemsetting-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        _tempRoots.Add(contentRoot);

        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns(contentRoot);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(options ?? new FileUploadOptions());

        var svc = new SystemSettingService(ctx.Factory, cache, env, opts);
        return (svc, contentRoot, cache);
    }

    private static void SeedSettings(SqliteTestContext ctx, params (string Key, string? Value)[] rows)
    {
        using var db = ctx.NewContext();
        foreach (var (key, value) in rows)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        }
        db.SaveChanges();
    }

    private static string? RowValue(SqliteTestContext ctx, string key)
    {
        using var db = ctx.NewContext();
        return db.SystemSettings.Where(e => e.Key == key).Select(e => e.Value).FirstOrDefault();
    }

    private static SystemConfigurationInput ValidInput() => new()
    {
        MaintenanceModeActive = true,
        MaintenanceModeText = "Down for maintenance",
        BannerText = "Test Banner",
        BannerLevel = BannerLevels.Warning,
        ThemePrimary = "#22D3EE",
        ThemeSecondary = "#0EA5E9",
        ThemeTertiary = "#334155",
        DemoModeActive = false,
        WantedBoardMinHazard = HazardLevel.High,
        MeetingWindowSize = 6,
        MeetingAnomalyYellow = 2,
        MeetingAnomalyRed = 4,
    };

    private static ClaimsPrincipal Admin() => ClaimsPrincipalBuilder.Agent("admin-1").AsAdmin().Build();

    // --- GetAsync (read-only, no guard) ---

    [Fact]
    public async Task GetAsync_NoRows_ReturnsCodeDefaults()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);

        var cfg = await svc.GetAsync();

        Assert.False(cfg.MaintenanceModeActive);
        Assert.Null(cfg.MaintenanceModeText);
        Assert.Null(cfg.BannerText);
        Assert.Equal(BannerLevels.Info, cfg.BannerLevel);
        Assert.False(cfg.DemoModeActive);
        Assert.False(cfg.HasLogo);
        Assert.Equal(HazardLevel.Critical, cfg.WantedBoardMinHazard);
        Assert.Equal(MeetingAnomalyDefaults.WindowSize, cfg.MeetingWindowSize);
        Assert.Equal(MeetingAnomalyDefaults.Yellow, cfg.MeetingAnomalyYellow);
        Assert.Equal(MeetingAnomalyDefaults.Red, cfg.MeetingAnomalyRed);
    }

    [Fact]
    public async Task GetAsync_ReadsPersistedValues()
    {
        using var ctx = new SqliteTestContext();
        SeedSettings(ctx,
            (SystemSettingKeys.MaintenanceModeActive, "true"),
            (SystemSettingKeys.MaintenanceModeText, "Wartung"),
            (SystemSettingKeys.BannerText, "Hinweis"),
            (SystemSettingKeys.BannerLevel, BannerLevels.Error),
            (SystemSettingKeys.ThemePrimary, "#22D3EE"),
            (SystemSettingKeys.DemoModeActive, "true"),
            (SystemSettingKeys.WantedBoardMinHazard, nameof(HazardLevel.High)),
            (SystemSettingKeys.LogoFileName, "logo-abc.png"),
            (SystemSettingKeys.LogoContentType, "image/png"));
        var (svc, _, _) = Build(ctx);

        var cfg = await svc.GetAsync();

        Assert.True(cfg.MaintenanceModeActive);
        Assert.Equal("Wartung", cfg.MaintenanceModeText);
        Assert.Equal("Hinweis", cfg.BannerText);
        Assert.Equal(BannerLevels.Error, cfg.BannerLevel);
        Assert.Equal("#22D3EE", cfg.ThemePrimary);
        Assert.True(cfg.DemoModeActive);
        Assert.Equal(HazardLevel.High, cfg.WantedBoardMinHazard);
        Assert.True(cfg.HasLogo);
        Assert.Equal("logo-abc.png", cfg.LogoFileName);
        Assert.Equal("image/png", cfg.LogoContentType);
    }

    [Fact]
    public async Task GetAsync_UnknownHazard_FallsBackToCritical()
    {
        using var ctx = new SqliteTestContext();
        SeedSettings(ctx, (SystemSettingKeys.WantedBoardMinHazard, "not-a-level"));
        var (svc, _, _) = Build(ctx);

        var cfg = await svc.GetAsync();

        Assert.Equal(HazardLevel.Critical, cfg.WantedBoardMinHazard);
    }

    [Fact]
    public async Task GetAsync_NormalisesIncoherentAnomalyThresholds()
    {
        using var ctx = new SqliteTestContext();
        // window(1) < red(2) < yellow(10) is incoherent -> Coherent clamps all to 10
        SeedSettings(ctx,
            (SystemSettingKeys.MeetingWindowSize, "1"),
            (SystemSettingKeys.MeetingAnomalyYellow, "10"),
            (SystemSettingKeys.MeetingAnomalyRed, "2"));
        var (svc, _, _) = Build(ctx);

        var cfg = await svc.GetAsync();

        Assert.Equal(10, cfg.MeetingAnomalyYellow);
        Assert.Equal(10, cfg.MeetingAnomalyRed);
        Assert.Equal(10, cfg.MeetingWindowSize);
    }

    [Fact]
    public async Task GetAsync_CachesResult_SubsequentDbChangesNotSeen()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);

        var first = await svc.GetAsync();
        Assert.False(first.MaintenanceModeActive);

        // direct DB change after the value was cached; GetAsync should keep returning the cached snapshot
        SeedSettings(ctx, (SystemSettingKeys.MaintenanceModeActive, "true"));

        var second = await svc.GetAsync();
        Assert.False(second.MaintenanceModeActive);
    }

    // --- SaveAsync (Permission.RequireAdmin) ---

    [Fact]
    public async Task SaveAsync_NonAdmin_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var actor = ClaimsPrincipalBuilder.Agent("u1").WithRank(Rank.Director).Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.SaveAsync(ValidInput(), actor));
    }

    [Fact]
    public async Task SaveAsync_PersistsAllValues()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);

        await svc.SaveAsync(ValidInput(), Admin());

        Assert.Equal("true", RowValue(ctx, SystemSettingKeys.MaintenanceModeActive));
        Assert.Equal("Down for maintenance", RowValue(ctx, SystemSettingKeys.MaintenanceModeText));
        Assert.Equal("Test Banner", RowValue(ctx, SystemSettingKeys.BannerText));
        Assert.Equal(BannerLevels.Warning, RowValue(ctx, SystemSettingKeys.BannerLevel));
        Assert.Equal("#22D3EE", RowValue(ctx, SystemSettingKeys.ThemePrimary));
        Assert.Equal("#0EA5E9", RowValue(ctx, SystemSettingKeys.ThemeSecondary));
        Assert.Equal("#334155", RowValue(ctx, SystemSettingKeys.ThemeTertiary));
        Assert.Equal("false", RowValue(ctx, SystemSettingKeys.DemoModeActive));
        Assert.Equal(nameof(HazardLevel.High), RowValue(ctx, SystemSettingKeys.WantedBoardMinHazard));
        Assert.Equal("6", RowValue(ctx, SystemSettingKeys.MeetingWindowSize));
        Assert.Equal("2", RowValue(ctx, SystemSettingKeys.MeetingAnomalyYellow));
        Assert.Equal("4", RowValue(ctx, SystemSettingKeys.MeetingAnomalyRed));
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingRowsWithoutDuplicating()
    {
        using var ctx = new SqliteTestContext();
        SeedSettings(ctx, (SystemSettingKeys.MaintenanceModeActive, "false"));
        var (svc, _, _) = Build(ctx);

        await svc.SaveAsync(ValidInput(), Admin());

        using var db = ctx.NewContext();
        var rows = db.SystemSettings.Where(e => e.Key == SystemSettingKeys.MaintenanceModeActive).ToList();
        Assert.Single(rows);
        Assert.Equal("true", rows[0].Value);
    }

    [Fact]
    public async Task SaveAsync_InvalidHexColour_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var input = ValidInput();
        input.ThemePrimary = "notacolour";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SaveAsync(input, Admin()));
    }

    [Fact]
    public async Task SaveAsync_InvalidBannerLevel_FallsBackToInfo()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var input = ValidInput();
        input.BannerLevel = "bogus";

        await svc.SaveAsync(input, Admin());

        Assert.Equal(BannerLevels.Info, RowValue(ctx, SystemSettingKeys.BannerLevel));
    }

    [Fact]
    public async Task SaveAsync_NormalisesAnomalyThresholds()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var input = ValidInput();
        // window(1) < yellow(9), red(3) < yellow -> Coherent clamps to (9,9,9)
        input.MeetingWindowSize = 1;
        input.MeetingAnomalyYellow = 9;
        input.MeetingAnomalyRed = 3;

        await svc.SaveAsync(input, Admin());

        Assert.Equal("9", RowValue(ctx, SystemSettingKeys.MeetingWindowSize));
        Assert.Equal("9", RowValue(ctx, SystemSettingKeys.MeetingAnomalyYellow));
        Assert.Equal("9", RowValue(ctx, SystemSettingKeys.MeetingAnomalyRed));
    }

    [Fact]
    public async Task SaveAsync_DemoFlipByNonBootstrapAdmin_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var input = ValidInput();
        input.DemoModeActive = true; // current is unset (false) -> flip requires bootstrap admin

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.SaveAsync(input, Admin()));
    }

    [Fact]
    public async Task SaveAsync_DemoFlipByBootstrapAdmin_Persists()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var input = ValidInput();
        input.DemoModeActive = true;
        var actor = ClaimsPrincipalBuilder.Agent("boot-1").AsAdmin().AsBootstrap().Build();

        await svc.SaveAsync(input, actor);

        Assert.Equal("true", RowValue(ctx, SystemSettingKeys.DemoModeActive));
    }

    [Fact]
    public async Task SaveAsync_InvalidatesCache()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);

        var before = await svc.GetAsync();
        Assert.False(before.MaintenanceModeActive);

        await svc.SaveAsync(ValidInput(), Admin());

        var after = await svc.GetAsync();
        Assert.True(after.MaintenanceModeActive);
        Assert.Equal("Test Banner", after.BannerText);
    }

    // --- LogoSetAsync (Permission.RequireAdmin) ---

    [Fact]
    public async Task LogoSetAsync_NonAdmin_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var actor = ClaimsPrincipalBuilder.Agent("u1").WithRank(Rank.Director).Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.LogoSetAsync(content, "crest.png", "image/png", 3, actor));
    }

    [Fact]
    public async Task LogoSetAsync_InvalidContentType_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.LogoSetAsync(content, "doc.pdf", "application/pdf", 3, Admin()));
    }

    [Fact]
    public async Task LogoSetAsync_TooLarge_Throws()
    {
        using var ctx = new SqliteTestContext();
        var opts = new FileUploadOptions { MaxBytes = 10 };
        var (svc, _, _) = Build(ctx, opts);
        using var content = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.LogoSetAsync(content, "crest.png", "image/png", sizeBytes: 11, Admin()));
    }

    [Fact]
    public async Task LogoSetAsync_PersistsFileAndSettings()
    {
        using var ctx = new SqliteTestContext();
        var (svc, contentRoot, _) = Build(ctx);
        var bytes = new byte[] { 10, 20, 30, 40 };
        using var content = new MemoryStream(bytes);

        await svc.LogoSetAsync(content, "crest.png", "image/png", bytes.Length, Admin());

        var fileName = RowValue(ctx, SystemSettingKeys.LogoFileName);
        Assert.NotNull(fileName);
        Assert.StartsWith("logo-", fileName);
        Assert.EndsWith(".png", fileName);
        Assert.Equal("image/png", RowValue(ctx, SystemSettingKeys.LogoContentType));

        var path = Path.Combine(contentRoot, "App_Data", "uploads", "system", fileName!);
        Assert.True(File.Exists(path));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task LogoSetAsync_ReplacingLogo_DeletesPreviousFile()
    {
        using var ctx = new SqliteTestContext();
        var (svc, contentRoot, _) = Build(ctx);

        using (var first = new MemoryStream(new byte[] { 1 }))
        {
            await svc.LogoSetAsync(first, "a.png", "image/png", 1, Admin());
        }
        var firstName = RowValue(ctx, SystemSettingKeys.LogoFileName);
        var firstPath = Path.Combine(contentRoot, "App_Data", "uploads", "system", firstName!);
        Assert.True(File.Exists(firstPath));

        using (var second = new MemoryStream(new byte[] { 2 }))
        {
            await svc.LogoSetAsync(second, "b.png", "image/png", 1, Admin());
        }

        var secondName = RowValue(ctx, SystemSettingKeys.LogoFileName);
        Assert.NotEqual(firstName, secondName);
        Assert.False(File.Exists(firstPath));
        Assert.True(File.Exists(Path.Combine(contentRoot, "App_Data", "uploads", "system", secondName!)));
    }

    // --- LogoRemoveAsync (Permission.RequireAdmin) ---

    [Fact]
    public async Task LogoRemoveAsync_NonAdmin_Throws()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var actor = ClaimsPrincipalBuilder.Agent("u1").WithRank(Rank.Director).Build();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.LogoRemoveAsync(actor));
    }

    [Fact]
    public async Task LogoRemoveAsync_ClearsSettingsAndDeletesFile()
    {
        using var ctx = new SqliteTestContext();
        var (svc, contentRoot, _) = Build(ctx);
        var bytes = new byte[] { 7, 7, 7 };
        using (var content = new MemoryStream(bytes))
        {
            await svc.LogoSetAsync(content, "crest.png", "image/png", bytes.Length, Admin());
        }
        var fileName = RowValue(ctx, SystemSettingKeys.LogoFileName)!;
        var path = Path.Combine(contentRoot, "App_Data", "uploads", "system", fileName);
        Assert.True(File.Exists(path));

        await svc.LogoRemoveAsync(Admin());

        Assert.Null(RowValue(ctx, SystemSettingKeys.LogoFileName));
        Assert.Null(RowValue(ctx, SystemSettingKeys.LogoContentType));
        Assert.False(File.Exists(path));
    }

    // --- LogoOpenAsync (read-only) ---

    [Fact]
    public async Task LogoOpenAsync_NoLogo_ReturnsNull()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);

        var opened = await svc.LogoOpenAsync();

        Assert.Null(opened);
    }

    [Fact]
    public async Task LogoOpenAsync_ReturnsStreamAndContentType()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _, _) = Build(ctx);
        var bytes = new byte[] { 42, 43, 44, 45, 46 };
        using (var content = new MemoryStream(bytes))
        {
            await svc.LogoSetAsync(content, "crest.png", "image/png", bytes.Length, Admin());
        }

        var opened = await svc.LogoOpenAsync();

        Assert.NotNull(opened);
        var (stream, contentType) = opened!.Value;
        Assert.Equal("image/png", contentType);
        using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            Assert.Equal(bytes, ms.ToArray());
        }
    }

    public void Dispose()
    {
        foreach (var dir in _tempRoots)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch { /* best effort */ }
        }
    }
}
