using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for ProfileSuggestionService over in-memory SQLite.</summary>
public class ProfileSuggestionServiceTests
{
    private static ProfileSuggestionService NewService(SqliteTestContext ctx) => new(ctx.Factory);

    private static void Seed(SqliteTestContext ctx, params ProfileSuggestion[] rows)
    {
        using var db = ctx.NewContext();
        db.ProfileSuggestions.AddRange(rows);
        db.SaveChanges();
    }

    // ---- GetAsync ----

    [Fact]
    public async Task GetAsync_ReturnsOnlyMatchingType_OrderedByValue()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx,
            new ProfileSuggestion { Type = SuggestionType.Weapon, Value = "Glock" },
            new ProfileSuggestion { Type = SuggestionType.Weapon, Value = "AK-47" },
            new ProfileSuggestion { Type = SuggestionType.Weapon, Value = "Deagle" },
            new ProfileSuggestion { Type = SuggestionType.Vehicle, Value = "Sultan" });

        var svc = NewService(ctx);
        var result = await svc.GetAsync(SuggestionType.Weapon);

        // alphabetical, only weapons
        Assert.Equal(new[] { "AK-47", "Deagle", "Glock" }, result.ToArray());
    }

    [Fact]
    public async Task GetAsync_ReturnsEmpty_WhenNoneOfType()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx, new ProfileSuggestion { Type = SuggestionType.Vehicle, Value = "Sultan" });

        var svc = NewService(ctx);
        var result = await svc.GetAsync(SuggestionType.Weapon);

        Assert.Empty(result);
    }

    // ---- StageAsync ----

    [Fact]
    public async Task StageAsync_AddsNewValues_OncCallerSaves()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        using (var db = ctx.NewContext())
        {
            await svc.StageAsync(db, SuggestionType.Location, new[] { "Sandy Shores", "Paleto Bay" });
            await db.SaveChangesAsync();
        }

        using var check = ctx.NewContext();
        var stored = check.ProfileSuggestions
            .Where(v => v.Type == SuggestionType.Location)
            .Select(v => v.Value)
            .OrderBy(v => v)
            .ToList();
        Assert.Equal(new[] { "Paleto Bay", "Sandy Shores" }, stored.ToArray());
    }

    [Fact]
    public async Task StageAsync_TrimsAndDropsEmptyValues()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        using (var db = ctx.NewContext())
        {
            await svc.StageAsync(db, SuggestionType.Kind, new[] { "  Dealer  ", "   ", "", "Runner" });
            await db.SaveChangesAsync();
        }

        using var check = ctx.NewContext();
        var stored = check.ProfileSuggestions
            .Where(v => v.Type == SuggestionType.Kind)
            .Select(v => v.Value)
            .OrderBy(v => v)
            .ToList();
        Assert.Equal(new[] { "Dealer", "Runner" }, stored.ToArray());
    }

    [Fact]
    public async Task StageAsync_DedupesWithinCall_CaseInsensitive_KeepsFirstCasing()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        using (var db = ctx.NewContext())
        {
            await svc.StageAsync(db, SuggestionType.Weapon, new[] { "Glock", "glock", "GLOCK" });
            await db.SaveChangesAsync();
        }

        using var check = ctx.NewContext();
        var stored = check.ProfileSuggestions
            .Where(v => v.Type == SuggestionType.Weapon)
            .Select(v => v.Value)
            .ToList();
        Assert.Single(stored);
        Assert.Equal("Glock", stored[0]);
    }

    [Fact]
    public async Task StageAsync_SkipsExistingValues_CaseInsensitive()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx, new ProfileSuggestion { Type = SuggestionType.Weapon, Value = "Glock" });

        var svc = NewService(ctx);
        using (var db = ctx.NewContext())
        {
            await svc.StageAsync(db, SuggestionType.Weapon, new[] { "glock", "Deagle" });
            await db.SaveChangesAsync();
        }

        using var check = ctx.NewContext();
        var stored = check.ProfileSuggestions
            .Where(v => v.Type == SuggestionType.Weapon)
            .Select(v => v.Value)
            .OrderBy(v => v)
            .ToList();
        // "glock" is skipped (already present), only "Deagle" is added
        Assert.Equal(new[] { "Deagle", "Glock" }, stored.ToArray());
    }

    [Fact]
    public async Task StageAsync_TreatsSameValueUnderDifferentTypeAsNew()
    {
        using var ctx = new SqliteTestContext();
        Seed(ctx, new ProfileSuggestion { Type = SuggestionType.Weapon, Value = "Bat" });

        var svc = NewService(ctx);
        using (var db = ctx.NewContext())
        {
            // same value text, different type -> not filtered out by the type-scoped exists check
            await svc.StageAsync(db, SuggestionType.Inventory, new[] { "Bat" });
            await db.SaveChangesAsync();
        }

        using var check = ctx.NewContext();
        Assert.Equal(1, check.ProfileSuggestions.Count(v => v.Type == SuggestionType.Inventory && v.Value == "Bat"));
        Assert.Equal(1, check.ProfileSuggestions.Count(v => v.Type == SuggestionType.Weapon && v.Value == "Bat"));
    }

    [Fact]
    public async Task StageAsync_NoOp_WhenAllValuesBlank()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        using (var db = ctx.NewContext())
        {
            await svc.StageAsync(db, SuggestionType.Location, new[] { "", "   ", "\t" });
            await db.SaveChangesAsync();
        }

        using var check = ctx.NewContext();
        Assert.Empty(check.ProfileSuggestions);
    }

    [Fact]
    public async Task StageAsync_OnlyStages_DoesNotPersistWithoutSave()
    {
        using var ctx = new SqliteTestContext();

        var svc = NewService(ctx);
        // deliberately do NOT SaveChanges on the caller's context
        using (var db = ctx.NewContext())
        {
            await svc.StageAsync(db, SuggestionType.Weapon, new[] { "Knife" });
        }

        // fresh context sees nothing since the caller never persisted
        using var check = ctx.NewContext();
        Assert.Empty(check.ProfileSuggestions);
    }
}
