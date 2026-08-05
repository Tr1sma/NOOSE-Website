using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Statistics;
using NOOSE_Website.Tests.Infrastructure;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="InventoryStatisticsService"/> over in-memory SQLite.</summary>
public sealed class InventoryStatisticsServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static InventoryStatisticsService Build(SqliteTestContext ctx,
        IReadOnlyDictionary<string, RecencySettings>? settings = null)
    {
        var recency = Substitute.For<IRecencyService>();
        recency.GetAllSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(settings ?? new Dictionary<string, RecencySettings>
            {
                ["Person"] = new(30, 90, false),
            });
        // a real cache, so the key/TTL path is exercised rather than stubbed away
        return new InventoryStatisticsService(ctx.Factory, recency, new MemoryCache(new MemoryCacheOptions()));
    }

    private static StatisticsScope Scope(bool includeClassified = true,
        StatisticsRange range = StatisticsRange.Months12) => new(includeClassified, range);

    /// <summary>Sum across every bucket of a series.</summary>
    private static double Total(ChartGrid grid, string series)
        => grid.Series.Single(s => s.Name == series).Values.Sum();

    [Fact]
    public async Task GetClassificationAsync_ProjectsEveryStageInDisplayOrder()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "A", configure: p => p.Classification = Classification.ReviewCase));
            db.People.Add(Seed.Person(name: "B", configure: p => p.Classification = Classification.ReviewCase));
            db.People.Add(Seed.Person(name: "C", configure: p => p.Classification = Classification.SuspicionCase));
            db.SaveChanges();
        }

        var grid = await svc.GetClassificationAsync(Scope());

        // deterministic order keeps colour bound to category
        Assert.Equal(ClassificationDisplay.All.Select(ClassificationDisplay.Name).ToList(), grid.Labels);
        var values = grid.Series.Single().Values;
        Assert.Equal(2, values[ClassificationDisplay.All.ToList().IndexOf(Classification.ReviewCase)]);
        Assert.Equal(1, values[ClassificationDisplay.All.ToList().IndexOf(Classification.SuspicionCase)]);
        Assert.Equal(0, values[ClassificationDisplay.All.ToList().IndexOf(Classification.SecuredStateThreatening)]);
    }

    [Fact]
    public async Task GetClassificationAsync_WithoutClassifiedAccess_ExcludesClassifiedPeople()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "offen", configure: p =>
            {
                p.Classification = Classification.ReviewCase;
            }));
            db.People.Add(Seed.Person(name: "geheim", configure: p =>
            {
                p.Classification = Classification.ReviewCase;
                p.IsClassified = true;
            }));
            db.SaveChanges();
        }

        var open = await svc.GetClassificationAsync(Scope(includeClassified: false));
        var all = await svc.GetClassificationAsync(Scope(includeClassified: true));

        Assert.Equal(1, Total(open, "Personen"));
        Assert.Equal(2, Total(all, "Personen"));
    }

    [Fact]
    public async Task GetClassificationAsync_SoftDeletedPerson_IsExcluded()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "lebt", configure: p => p.Classification = Classification.ReviewCase));
            db.People.Add(Seed.Person(name: "weg", configure: p =>
            {
                p.Classification = Classification.ReviewCase;
                p.IsDeleted = true;
            }));
            db.SaveChanges();
        }

        var grid = await svc.GetClassificationAsync(Scope());

        Assert.Equal(1, Total(grid, "Personen"));
    }

    [Fact]
    public async Task GetClassificationAsync_EmptyDatabase_StillReturnsFullAxis()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        var grid = await svc.GetClassificationAsync(Scope());

        // a chart must never receive a collapsed axis, even with nothing in it
        Assert.Equal(ClassificationDisplay.All.Count, grid.Labels.Count);
        Assert.True(grid.IsEmpty);
        Assert.All(grid.Series.Single().Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public async Task GetHazardComparisonAsync_BucketsBothPopulationsByBand()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "kritisch", configure: p => p.ThreatScore = 80));
            db.People.Add(Seed.Person(name: "hoch", configure: p => p.ThreatScore = 60));
            db.Factions.Add(Seed.Faction(name: "mittel", configure: f => f.ThreatScore = 30));
            db.SaveChanges();
        }

        var grid = await svc.GetHazardComparisonAsync(Scope());
        var levels = HazardLevelLogic.All.ToList();

        Assert.Equal(levels.Select(HazardLevelLogic.Name).ToList(), grid.Labels);
        Assert.Equal(1, grid.Series.Single(s => s.Name == "Personen").Values[levels.IndexOf(HazardLevel.Critical)]);
        Assert.Equal(1, grid.Series.Single(s => s.Name == "Personen").Values[levels.IndexOf(HazardLevel.High)]);
        Assert.Equal(1, grid.Series.Single(s => s.Name == "Fraktionen").Values[levels.IndexOf(HazardLevel.Medium)]);
    }

    [Theory]
    [InlineData(0, HazardLevel.No)]
    [InlineData(24, HazardLevel.Low)]
    [InlineData(25, HazardLevel.Medium)]
    [InlineData(49, HazardLevel.Medium)]
    [InlineData(50, HazardLevel.High)]
    [InlineData(74, HazardLevel.High)]
    [InlineData(75, HazardLevel.Critical)]
    [InlineData(100, HazardLevel.Critical)]
    public async Task GetHazardComparisonAsync_BandBoundaries_LandInTheExpectedBucket(int score, HazardLevel expected)
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "grenzwert", configure: p => p.ThreatScore = score));
            db.SaveChanges();
        }

        var grid = await svc.GetHazardComparisonAsync(Scope());
        var index = HazardLevelLogic.All.ToList().IndexOf(expected);

        Assert.Equal(1, grid.Series.Single(s => s.Name == "Personen").Values[index]);
    }

    [Fact]
    public async Task GetLifeStatusAsync_ExpiredDeathWindow_CountsAsAlive()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "wieder da", configure: p =>
            {
                p.LifeStatus = LifeStatus.Dead;
                p.DeadUntil = Now.AddMinutes(-5);
            }));
            db.People.Add(Seed.Person(name: "noch tot", configure: p =>
            {
                p.LifeStatus = LifeStatus.Dead;
                p.DeadUntil = Now.AddMinutes(15);
            }));
            db.SaveChanges();
        }

        var segments = await svc.GetLifeStatusAsync(Scope());

        Assert.Equal(LifeStatusDisplay.All.Select(LifeStatusDisplay.Name).ToList(),
            segments.Select(s => s.Designation).ToList());
        Assert.Equal(1, segments.Single(s => s.Designation == LifeStatusDisplay.Name(LifeStatus.Alive)).Count);
        Assert.Equal(1, segments.Single(s => s.Designation == LifeStatusDisplay.Name(LifeStatus.Dead)).Count);
    }

    [Fact]
    public async Task GetCaseFunnelAsync_ProjectsEveryStatusInWorkflowOrder()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Cases.Add(Seed.Case(title: "offen", configure: c => c.Status = CaseStatus.Open));
            db.Cases.Add(Seed.Case(title: "fertig", configure: c => c.Status = CaseStatus.Completed));
            db.SaveChanges();
        }

        var grid = await svc.GetCaseFunnelAsync(Scope());

        Assert.Equal(CaseStatusDisplay.All.Select(CaseStatusDisplay.Name).ToList(), grid.Labels);
        Assert.Equal(2, Total(grid, "Vorgänge"));
    }

    [Fact]
    public async Task GetGrowthAsync_CountsOnlyRecordsInsideTheWindow()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "neu", configure: p => p.CreatedAt = Now.AddDays(-2)));
            db.People.Add(Seed.Person(name: "alt", configure: p => p.CreatedAt = Now.AddYears(-3)));
            db.SaveChanges();
        }

        var grid = await svc.GetGrowthAsync(Scope(range: StatisticsRange.Months12));

        Assert.Equal(12, grid.Labels.Count);
        Assert.Equal(1, Total(grid, "Personen"));
    }

    [Fact]
    public async Task GetGrowthAsync_ShortRange_BucketsPerDay()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "heute", configure: p => p.CreatedAt = Now));
            db.SaveChanges();
        }

        var grid = await svc.GetGrowthAsync(Scope(range: StatisticsRange.Days30));

        // 30 daily buckets, and today's record lands in the last one
        Assert.Equal(30, grid.Labels.Count);
        Assert.Equal(1, grid.Series.Single(s => s.Name == "Personen").Values[^1]);
    }

    [Fact]
    public async Task GetMeasureOutcomeTrendAsync_ClassifiedParent_IsHiddenWithoutAccess()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        using (var db = ctx.NewContext())
        {
            var visible = Seed.Person(name: "offen");
            var secret = Seed.Person(name: "geheim", configure: p => p.IsClassified = true);
            db.People.AddRange(visible, secret);
            db.PersonDocs.Add(new NOOSE_Website.Data.Entities.People.PersonDoc
            {
                Id = Guid.NewGuid().ToString(), PersonId = visible.Id,
                Timestamp = Now.AddDays(-1), Outcome = MeasureOutcome.Shot,
            });
            db.PersonDocs.Add(new NOOSE_Website.Data.Entities.People.PersonDoc
            {
                Id = Guid.NewGuid().ToString(), PersonId = secret.Id,
                Timestamp = Now.AddDays(-1), Outcome = MeasureOutcome.Shot,
            });
            db.SaveChanges();
        }

        var open = await svc.GetMeasureOutcomeTrendAsync(Scope(includeClassified: false));
        var all = await svc.GetMeasureOutcomeTrendAsync(Scope(includeClassified: true));

        Assert.Equal(1, Total(open, MeasureOutcomeDisplay.Name(MeasureOutcome.Shot)));
        Assert.Equal(2, Total(all, MeasureOutcomeDisplay.Name(MeasureOutcome.Shot)));
    }

    [Fact]
    public async Task GetRecencyAsync_SplitsFreshFromStaleByTheTypeThreshold()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx, new Dictionary<string, RecencySettings> { ["Person"] = new(30, 90, false) });

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "frisch", configure: p =>
            {
                p.CreatedAt = Now.AddDays(-5);
                p.ModifiedAt = Now.AddDays(-5);
            }));
            db.People.Add(Seed.Person(name: "alt", configure: p =>
            {
                p.CreatedAt = Now.AddDays(-200);
                p.ModifiedAt = null;
            }));
            db.SaveChanges();
        }

        var rows = await svc.GetRecencyAsync(Scope());
        var people = rows.Single(r => r.Label == "Personen");

        Assert.Equal(2, people.Total);
        Assert.Equal(1, people.Value);
        Assert.Equal(0.5, people.Share);
    }

    [Fact]
    public async Task GetRecencyAsync_AgingDisabled_CountsEverythingAsFresh()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx, new Dictionary<string, RecencySettings> { ["Person"] = new(30, 90, true) });

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "uralt", configure: p =>
            {
                p.CreatedAt = Now.AddYears(-5);
                p.ModifiedAt = null;
            }));
            db.SaveChanges();
        }

        var people = (await svc.GetRecencyAsync(Scope())).Single(r => r.Label == "Personen");

        Assert.Equal(1, people.Value);
        Assert.Equal(1, people.Total);
    }

    [Fact]
    public async Task GetRecencyAsync_TypeWithoutRecords_IsOmitted()
    {
        using var ctx = new SqliteTestContext();
        var svc = Build(ctx);

        var rows = await svc.GetRecencyAsync(Scope());

        // an empty meter row would read as "0 % current", which is misleading
        Assert.Empty(rows);
    }
}
