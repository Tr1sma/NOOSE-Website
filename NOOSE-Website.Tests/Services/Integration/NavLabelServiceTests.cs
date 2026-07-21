using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services.Integration;

/// <summary>Integration tests for <see cref="NavLabelService" /> against in-memory SQLite.</summary>
public sealed class NavLabelServiceTests : IDisposable
{
    private readonly SqliteTestContext _ctx = new();

    private NavLabelService CreateService() => new(_ctx.Factory);

    // internal viewer without classified-read
    private static ViewerScope Regular() => new(false, false, "me", null);

    // classified-read (leadership) viewer
    private static ViewerScope Leadership() => new(true, true, "me", null);

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task ResolveAsync_NullPath_ReturnsDashboardSection()
    {
        var svc = CreateService();

        var loc = await svc.ResolveAsync(null, Regular());

        Assert.Equal("dashboard", loc.Section?.Key);
        Assert.False(loc.IsRecord);
        Assert.Null(loc.EntityType);
        Assert.Null(loc.EntityId);
    }

    [Fact]
    public async Task ResolveAsync_EmptyPath_ReturnsDashboardSection()
    {
        var svc = CreateService();

        var loc = await svc.ResolveAsync("", Regular());

        Assert.Equal("dashboard", loc.Section?.Key);
        Assert.False(loc.IsRecord);
        Assert.Equal("/", loc.Route);
    }

    [Fact]
    public async Task ResolveAsync_ListRoute_ReturnsSectionOnly()
    {
        var svc = CreateService();

        var loc = await svc.ResolveAsync("personen", Regular());

        Assert.Equal("personen", loc.Section?.Key);
        Assert.False(loc.IsRecord);
        Assert.Null(loc.EntityType);
        Assert.Equal("/personen", loc.Route);
    }

    [Fact]
    public async Task ResolveAsync_UnknownPrefixWithSegment_ReturnsSectionOnly()
    {
        var svc = CreateService();

        // "kalender" is a real section but not a record-type prefix
        var loc = await svc.ResolveAsync("kalender/whatever", Regular());

        Assert.Equal("kalender", loc.Section?.Key);
        Assert.False(loc.IsRecord);
        Assert.Null(loc.EntityType);
    }

    [Fact]
    public async Task ResolveAsync_VisiblePerson_ReturnsRecordLocation()
    {
        using (var db = _ctx.NewContext())
        {
            db.People.Add(Seed.Person("p1", "Max Mustermann"));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("personen/p1", Regular());

        Assert.True(loc.IsRecord);
        Assert.Equal("Person", loc.EntityType);
        Assert.Equal("p1", loc.EntityId);
        Assert.Equal("Max Mustermann", loc.RecordName);
        Assert.Equal("/personen/p1", loc.Route);
        Assert.Equal("personen", loc.Section?.Key);
    }

    [Fact]
    public async Task ResolveAsync_StripsQueryAndFragment()
    {
        using (var db = _ctx.NewContext())
        {
            db.People.Add(Seed.Person("p2", "Erika Musterfrau"));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("personen/p2?tab=doks#top", Regular());

        Assert.True(loc.IsRecord);
        Assert.Equal("p2", loc.EntityId);
        Assert.Equal("Erika Musterfrau", loc.RecordName);
        Assert.Equal("/personen/p2", loc.Route);
    }

    [Fact]
    public async Task ResolveAsync_LeadingSlashPath_Resolves()
    {
        using (var db = _ctx.NewContext())
        {
            db.People.Add(Seed.Person("p3", "Slash Person"));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("/personen/p3", Regular());

        Assert.True(loc.IsRecord);
        Assert.Equal("p3", loc.EntityId);
        Assert.Equal("Slash Person", loc.RecordName);
    }

    [Fact]
    public async Task ResolveAsync_ClassifiedPerson_NotVisibleToRegularViewer_ReturnsSectionOnly()
    {
        using (var db = _ctx.NewContext())
        {
            db.People.Add(Seed.Person("pc", "Geheim", p => p.IsClassified = true));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("personen/pc", Regular());

        // never leak a name the viewer cannot see
        Assert.False(loc.IsRecord);
        Assert.Null(loc.EntityType);
        Assert.Null(loc.RecordName);
        Assert.Equal("personen", loc.Section?.Key);
    }

    [Fact]
    public async Task ResolveAsync_ClassifiedPerson_VisibleToLeadership_ReturnsRecordLocation()
    {
        using (var db = _ctx.NewContext())
        {
            db.People.Add(Seed.Person("pc2", "Geheim Sichtbar", p => p.IsClassified = true));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("personen/pc2", Leadership());

        Assert.True(loc.IsRecord);
        Assert.Equal("Person", loc.EntityType);
        Assert.Equal("Geheim Sichtbar", loc.RecordName);
    }

    [Fact]
    public async Task ResolveAsync_NonexistentRecord_ReturnsSectionOnly()
    {
        var svc = CreateService();

        var loc = await svc.ResolveAsync("personen/does-not-exist", Leadership());

        Assert.False(loc.IsRecord);
        Assert.Null(loc.EntityType);
        Assert.Equal("personen", loc.Section?.Key);
    }

    [Fact]
    public async Task ResolveAsync_Faction_ReturnsRecordName()
    {
        using (var db = _ctx.NewContext())
        {
            db.Factions.Add(Seed.Faction("f1", "Ballas"));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("fraktionen/f1", Regular());

        Assert.True(loc.IsRecord);
        Assert.Equal("Faction", loc.EntityType);
        Assert.Equal("f1", loc.EntityId);
        Assert.Equal("Ballas", loc.RecordName);
        Assert.Equal("/fraktionen/f1", loc.Route);
    }

    [Fact]
    public async Task ResolveAsync_Case_ReturnsTitle()
    {
        using (var db = _ctx.NewContext())
        {
            db.Cases.Add(Seed.Case("c1", "Ermittlung Nord"));
            db.SaveChanges();
        }
        var svc = CreateService();

        var loc = await svc.ResolveAsync("vorgaenge/c1", Regular());

        Assert.True(loc.IsRecord);
        Assert.Equal("Case", loc.EntityType);
        Assert.Equal("c1", loc.EntityId);
        Assert.Equal("Ermittlung Nord", loc.RecordName);
        Assert.Equal("/vorgaenge/c1", loc.Route);
    }

    [Fact]
    public async Task ResolveAsync_Law_ReturnsTitle()
    {
        using (var db = _ctx.NewContext())
        {
            db.Laws.Add(new Law { Id = "l1", Title = "Paragraf 1" });
            db.SaveChanges();
        }
        var svc = CreateService();

        // Law is an "always visible" type (gated on existence only)
        var loc = await svc.ResolveAsync("gesetze/l1", Regular());

        Assert.True(loc.IsRecord);
        Assert.Equal("Law", loc.EntityType);
        Assert.Equal("l1", loc.EntityId);
        Assert.Equal("Paragraf 1", loc.RecordName);
        Assert.Equal("/gesetze/l1", loc.Route);
    }
}
