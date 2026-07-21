using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Statistics;
using NSubstitute;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="StatisticsService"/> over in-memory SQLite.</summary>
public sealed class StatisticsServiceTests
{
    private static (StatisticsService Svc, IDashboardService Dashboard) Build(
        SqliteTestContext ctx, DashboardMetrics? metrics = null)
    {
        var dashboard = Substitute.For<IDashboardService>();
        dashboard.GetMetricsAsync(Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(metrics ?? new DashboardMetrics(0, 0, 0, 0, 0, 0, 0));
        return (new StatisticsService(ctx.Factory, dashboard), dashboard);
    }

    /// <summary>Count of the named segment; also proves the segment exists exactly once.</summary>
    private static int Seg(IReadOnlyList<DistributionSegment> segs, string name)
        => segs.Single(s => s.Designation == name).Count;

    // ---------- Metrics tile passthrough ----------

    [Fact]
    public async Task GetReportAsync_UsesDashboardMetricsFromCollaborator()
    {
        using var ctx = new SqliteTestContext();
        var metrics = new DashboardMetrics(5, 4, 3, 2, 1, 6, 7);
        var (svc, dashboard) = Build(ctx, metrics);

        var report = await svc.GetReportAsync(isLeadership: true, meId: "me-1");

        Assert.Equal(metrics, report.Metrics);
        await dashboard.Received(1).GetMetricsAsync(true, "me-1", Arg.Any<CancellationToken>());
    }

    // ---------- People by classification ----------

    [Fact]
    public async Task GetReportAsync_PeopleByClassification_OrdersAndCounts()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "U1", configure: p => { p.CaseNumber = "P-1"; p.Classification = Classification.Unknown; }));
            db.People.Add(Seed.Person(name: "U2", configure: p => { p.CaseNumber = "P-2"; p.Classification = Classification.Unknown; }));
            db.People.Add(Seed.Person(name: "R1", configure: p => { p.CaseNumber = "P-3"; p.Classification = Classification.ReviewCase; }));
            db.People.Add(Seed.Person(name: "S1", configure: p => { p.CaseNumber = "P-4"; p.Classification = Classification.SuspicionCase; }));
            db.People.Add(Seed.Person(name: "S2", configure: p => { p.CaseNumber = "P-5"; p.Classification = Classification.SuspicionCase; }));
            db.People.Add(Seed.Person(name: "S3", configure: p => { p.CaseNumber = "P-6"; p.Classification = Classification.SuspicionCase; }));
            db.SaveChanges();
        }

        var report = await svc.GetReportAsync(isLeadership: true, meId: null);
        var segs = report.PeopleByClassification;

        // Deterministic order = ClassificationDisplay.All.
        Assert.Equal(ClassificationDisplay.All.Count, segs.Count);
        Assert.Equal(
            ClassificationDisplay.All.Select(ClassificationDisplay.Name).ToList(),
            segs.Select(s => s.Designation).ToList());
        Assert.Equal(2, Seg(segs, "Unbekannt"));
        Assert.Equal(1, Seg(segs, "Prüffall"));
        Assert.Equal(3, Seg(segs, "Verdachtsfall"));
        Assert.Equal(0, Seg(segs, "Gesichert staatsgefährdend"));
    }

    [Fact]
    public async Task GetReportAsync_NonLeadership_ExcludesClassifiedPeople()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "Open", configure: p => { p.CaseNumber = "P-1"; p.Classification = Classification.SuspicionCase; }));
            db.People.Add(Seed.Person(name: "VS", configure: p => { p.CaseNumber = "P-2"; p.Classification = Classification.SuspicionCase; p.IsClassified = true; }));
            db.SaveChanges();
        }

        var forReader = await svc.GetReportAsync(isLeadership: false, meId: null);
        var forLeader = await svc.GetReportAsync(isLeadership: true, meId: null);

        // Reader never sees the classified record.
        Assert.Equal(1, Seg(forReader.PeopleByClassification, "Verdachtsfall"));
        // Leadership sees both.
        Assert.Equal(2, Seg(forLeader.PeopleByClassification, "Verdachtsfall"));
    }

    [Fact]
    public async Task GetReportAsync_SoftDeletedPerson_ExcludedFromCounts()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "Live", configure: p => { p.CaseNumber = "P-1"; p.Classification = Classification.Unknown; }));
            db.People.Add(Seed.Person(name: "Gone", configure: p => { p.CaseNumber = "P-2"; p.Classification = Classification.Unknown; p.IsDeleted = true; }));
            db.SaveChanges();
        }

        var report = await svc.GetReportAsync(isLeadership: true, meId: null);

        // Global soft-delete query filter drops the deleted row.
        Assert.Equal(1, Seg(report.PeopleByClassification, "Unbekannt"));
    }

    // ---------- People / factions by hazard ----------

    [Fact]
    public async Task GetReportAsync_PeopleByHazard_MapsScoresToLevels()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "N1", configure: p => { p.CaseNumber = "P-1"; p.ThreatScore = null; }));   // No
            db.People.Add(Seed.Person(name: "N2", configure: p => { p.CaseNumber = "P-2"; p.ThreatScore = 0; }));      // No
            db.People.Add(Seed.Person(name: "L1", configure: p => { p.CaseNumber = "P-3"; p.ThreatScore = 10; }));     // Low
            db.People.Add(Seed.Person(name: "M1", configure: p => { p.CaseNumber = "P-4"; p.ThreatScore = 30; }));     // Medium
            db.People.Add(Seed.Person(name: "H1", configure: p => { p.CaseNumber = "P-5"; p.ThreatScore = 60; }));     // High
            db.People.Add(Seed.Person(name: "C1", configure: p => { p.CaseNumber = "P-6"; p.ThreatScore = 90; }));     // Critical
            db.SaveChanges();
        }

        var segs = (await svc.GetReportAsync(isLeadership: true, meId: null)).PeopleByHazard;

        Assert.Equal(HazardLevelLogic.All.Count, segs.Count);
        Assert.Equal(
            HazardLevelLogic.All.Select(HazardLevelLogic.Name).ToList(),
            segs.Select(s => s.Designation).ToList());
        Assert.Equal(2, Seg(segs, "Keine"));
        Assert.Equal(1, Seg(segs, "Niedrig"));
        Assert.Equal(1, Seg(segs, "Mittel"));
        Assert.Equal(1, Seg(segs, "Hoch"));
        Assert.Equal(1, Seg(segs, "Kritisch"));
    }

    [Fact]
    public async Task GetReportAsync_FactionsByHazard_MapsScoresToLevels()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Factions.Add(Seed.Faction(name: "F0", configure: f => { f.CaseNumber = "F-1"; f.ThreatScore = null; })); // No
            db.Factions.Add(Seed.Faction(name: "F1", configure: f => { f.CaseNumber = "F-2"; f.ThreatScore = 20; }));   // Low
            db.Factions.Add(Seed.Faction(name: "F2", configure: f => { f.CaseNumber = "F-3"; f.ThreatScore = 80; }));   // Critical
            db.SaveChanges();
        }

        var segs = (await svc.GetReportAsync(isLeadership: true, meId: null)).FactionsByHazard;

        Assert.Equal(1, Seg(segs, "Keine"));
        Assert.Equal(1, Seg(segs, "Niedrig"));
        Assert.Equal(0, Seg(segs, "Mittel"));
        Assert.Equal(0, Seg(segs, "Hoch"));
        Assert.Equal(1, Seg(segs, "Kritisch"));
    }

    // ---------- People by life status ----------

    [Fact]
    public async Task GetReportAsync_PeopleByLifeStatus_ExpiredDeathCountsAsAlive()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var now = DateTime.UtcNow;
        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(name: "Alive", configure: p => { p.CaseNumber = "P-1"; p.LifeStatus = LifeStatus.Alive; }));
            db.People.Add(Seed.Person(name: "DeadActive", configure: p => { p.CaseNumber = "P-2"; p.LifeStatus = LifeStatus.Dead; p.DeadUntil = now.AddMinutes(10); }));
            db.People.Add(Seed.Person(name: "DeadExpired", configure: p => { p.CaseNumber = "P-3"; p.LifeStatus = LifeStatus.Dead; p.DeadUntil = now.AddMinutes(-10); }));
            db.People.Add(Seed.Person(name: "Fugitive", configure: p => { p.CaseNumber = "P-4"; p.LifeStatus = LifeStatus.Fugitive; }));
            db.SaveChanges();
        }

        var segs = (await svc.GetReportAsync(isLeadership: true, meId: null)).PeopleByLifeStatus;

        Assert.Equal(LifeStatusDisplay.All.Count, segs.Count);
        // Expired death window collapses to Alive -> 2 alive.
        Assert.Equal(2, Seg(segs, "Lebend"));
        Assert.Equal(1, Seg(segs, "Tot"));
        Assert.Equal(1, Seg(segs, "Flüchtig"));
    }

    // ---------- Measure outcomes ----------

    [Fact]
    public async Task GetReportAsync_MeasureOutcomes_CountsByOutcome()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "person-1", name: "Doc Owner", configure: p => p.CaseNumber = "P-1"));
            db.PersonDocs.Add(NewDoc("person-1", MeasureOutcome.RunningStill));
            db.PersonDocs.Add(NewDoc("person-1", MeasureOutcome.RunningStill));
            db.PersonDocs.Add(NewDoc("person-1", MeasureOutcome.Injection));
            db.PersonDocs.Add(NewDoc("person-1", MeasureOutcome.Shot));
            db.SaveChanges();
        }

        var segs = (await svc.GetReportAsync(isLeadership: true, meId: null)).MeasureOutcomes;

        Assert.Equal(MeasureOutcomeDisplay.All.Count, segs.Count);
        Assert.Equal(2, Seg(segs, "Läuft noch"));
        Assert.Equal(0, Seg(segs, "Offiziell entlassen"));
        Assert.Equal(1, Seg(segs, "Amnestie-Spritze"));
        Assert.Equal(1, Seg(segs, "Erschossen"));
    }

    [Fact]
    public async Task GetReportAsync_NonLeadership_ExcludesDocsOfClassifiedPersons()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "open", name: "Open", configure: p => p.CaseNumber = "P-1"));
            db.People.Add(Seed.Person(id: "vs", name: "VS", configure: p => { p.CaseNumber = "P-2"; p.IsClassified = true; }));
            db.PersonDocs.Add(NewDoc("open", MeasureOutcome.Shot));
            db.PersonDocs.Add(NewDoc("vs", MeasureOutcome.Shot));
            db.SaveChanges();
        }

        var forReader = await svc.GetReportAsync(isLeadership: false, meId: null);
        var forLeader = await svc.GetReportAsync(isLeadership: true, meId: null);

        // Reader only counts the doc on the non-classified person.
        Assert.Equal(1, Seg(forReader.MeasureOutcomes, "Erschossen"));
        Assert.Equal(2, Seg(forLeader.MeasureOutcomes, "Erschossen"));
    }

    // ---------- Cases by status ----------

    [Fact]
    public async Task GetReportAsync_CasesByStatus_CountsByStatus()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Cases.Add(Seed.Case(title: "C1", configure: c => { c.CaseNumber = "V-1"; c.Status = CaseStatus.Open; }));
            db.Cases.Add(Seed.Case(title: "C2", configure: c => { c.CaseNumber = "V-2"; c.Status = CaseStatus.Open; }));
            db.Cases.Add(Seed.Case(title: "C3", configure: c => { c.CaseNumber = "V-3"; c.Status = CaseStatus.InProcessing; }));
            db.Cases.Add(Seed.Case(title: "C4", configure: c => { c.CaseNumber = "V-4"; c.Status = CaseStatus.Completed; }));
            db.SaveChanges();
        }

        var segs = (await svc.GetReportAsync(isLeadership: true, meId: null)).CasesByStatus;

        Assert.Equal(CaseStatusDisplay.All.Count, segs.Count);
        Assert.Equal(2, Seg(segs, "Offen"));
        Assert.Equal(1, Seg(segs, "In Bearbeitung"));
        Assert.Equal(0, Seg(segs, "Ruht"));
        Assert.Equal(1, Seg(segs, "Abgeschlossen"));
        Assert.Equal(0, Seg(segs, "Archiviert"));
    }

    // ---------- Top threats ----------

    [Fact]
    public async Task GetReportAsync_TopPeople_OrderedExcludesUnscoredRespectsTopN()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.People.Add(Seed.Person(id: "p-alpha", name: "Alpha", configure: p => { p.CaseNumber = "P-1"; p.ThreatScore = 90; }));
            db.People.Add(Seed.Person(id: "p-beta", name: "Beta", configure: p => { p.CaseNumber = "P-2"; p.ThreatScore = 50; }));
            db.People.Add(Seed.Person(id: "p-echo", name: "Echo", configure: p => { p.CaseNumber = "P-3"; p.ThreatScore = 10; }));
            db.People.Add(Seed.Person(id: "p-null", name: "NullScore", configure: p => { p.CaseNumber = "P-4"; p.ThreatScore = null; }));
            db.People.Add(Seed.Person(id: "p-zero", name: "ZeroScore", configure: p => { p.CaseNumber = "P-5"; p.ThreatScore = 0; }));
            db.SaveChanges();
        }

        // topN caps the list; ordering is score desc.
        var capped = (await svc.GetReportAsync(isLeadership: true, meId: null, topN: 2)).TopPeople;
        Assert.Equal(2, capped.Count);
        Assert.Equal("Alpha", capped[0].Name);
        Assert.Equal(90, capped[0].Score);
        Assert.Equal(HazardLevel.Critical, capped[0].Level);
        Assert.Equal("/personen/p-alpha", capped[0].Href);
        Assert.Equal("Beta", capped[1].Name);
        Assert.Equal(HazardLevel.High, capped[1].Level);

        // Larger cap: exactly the three scored (>0) records, still ordered, null/zero excluded.
        var full = (await svc.GetReportAsync(isLeadership: true, meId: null, topN: 10)).TopPeople;
        Assert.Equal(new[] { "Alpha", "Beta", "Echo" }, full.Select(e => e.Name).ToArray());
    }

    [Fact]
    public async Task GetReportAsync_TopFactions_OrderedExcludesUnscored()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        using (var db = ctx.NewContext())
        {
            db.Factions.Add(Seed.Faction(id: "f-a", name: "FactionA", configure: f => { f.CaseNumber = "F-1"; f.ThreatScore = 80; }));
            db.Factions.Add(Seed.Faction(id: "f-b", name: "FactionB", configure: f => { f.CaseNumber = "F-2"; f.ThreatScore = 20; }));
            db.Factions.Add(Seed.Faction(id: "f-c", name: "FactionC", configure: f => { f.CaseNumber = "F-3"; f.ThreatScore = null; }));
            db.SaveChanges();
        }

        var top = (await svc.GetReportAsync(isLeadership: true, meId: null)).TopFactions;

        Assert.Equal(new[] { "FactionA", "FactionB" }, top.Select(e => e.Name).ToArray());
        Assert.Equal(80, top[0].Score);
        Assert.Equal(HazardLevel.Critical, top[0].Level);
        Assert.Equal("/fraktionen/f-a", top[0].Href);
    }

    // ---------- Time series ----------

    [Fact]
    public async Task GetReportAsync_TimeSeries_TwelveMonthsWithCurrentMonthCounts()
    {
        using var ctx = new SqliteTestContext();
        var (svc, _) = Build(ctx);

        var now = DateTime.UtcNow;
        using (var db = ctx.NewContext())
        {
            // A person created this month (a "new entry") and one measure this month.
            db.People.Add(Seed.Person(id: "person-now", name: "Recent", configure: p => { p.CaseNumber = "P-1"; p.CreatedAt = now; }));
            db.PersonDocs.Add(NewDoc("person-now", MeasureOutcome.RunningStill, timestamp: now));
            db.SaveChanges();
        }

        var series = (await svc.GetReportAsync(isLeadership: true, meId: null)).TimeSeries;

        Assert.Equal(12, series.Count);
        var current = series[^1];
        Assert.Equal(now.Year, current.Year);
        Assert.Equal(now.Month, current.Month);
        Assert.Equal(1, current.Measures);
        Assert.Equal(1, current.NewEntries);
    }

    private static PersonDoc NewDoc(string personId, MeasureOutcome outcome, DateTime? timestamp = null)
        => new()
        {
            PersonId = personId,
            Outcome = outcome,
            Timestamp = timestamp ?? new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = timestamp ?? new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        };
}
