using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The snapshot's own rules: effective state, nav order, and what the kill switch does to both.</summary>
public class PublicModuleSnapshotTests
{
    private static PublicModuleState Module(
        string key,
        bool enabled = true,
        string? route = null,
        int sortOrder = 0,
        bool available = true,
        PublicModuleGroup group = PublicModuleGroup.Fahndung,
        string? label = null)
        => new(
            Key: key,
            Label: label ?? key,
            Description: "…",
            Icon: "icon",
            NavRoute: route,
            Group: group,
            SortOrder: sortOrder,
            IsEnabled: enabled,
            OfflineText: "offline",
            Available: available);

    private static PublicModuleSnapshot Snapshot(bool kill, params PublicModuleState[] modules)
        => new(kill, modules);

    // ---- effective state ----

    [Fact]
    public void IsEnabled_ReflectsTheStoredSwitch()
    {
        var snapshot = Snapshot(false, Module("A", enabled: true), Module("B", enabled: false));

        Assert.True(snapshot.IsEnabled("A"));
        Assert.False(snapshot.IsEnabled("B"));
    }

    [Fact]
    public void IsEnabled_UnknownKey_IsFalse()
        => Assert.False(Snapshot(false, Module("A")).IsEnabled("C"));

    [Fact]
    public void IsEnabled_IsCaseSensitive()
    {
        // keys are code constants; a near-miss must not silently enable something
        var snapshot = Snapshot(false, Module("Fahndung"));

        Assert.True(snapshot.IsEnabled("Fahndung"));
        Assert.False(snapshot.IsEnabled("fahndung"));
    }

    [Fact]
    public void KillSwitch_TurnsEverythingOff_WithoutChangingTheStoredSwitch()
    {
        var snapshot = Snapshot(true, Module("A", enabled: true));

        Assert.False(snapshot.IsEnabled("A"));
        Assert.True(snapshot.Find("A")!.IsEnabled);
    }

    // ---- nav ----

    [Fact]
    public void NavEntries_AreOrderedBySortOrder()
    {
        var snapshot = Snapshot(false,
            Module("Late", route: "/late", sortOrder: 900),
            Module("Early", route: "/early", sortOrder: 5),
            Module("Middle", route: "/middle", sortOrder: 100));

        var keys = snapshot.NavEntries().Select(e => e.Key).ToArray();

        Assert.Equal(new[] { "Early", "Middle", "Late" }, keys);
    }

    [Fact]
    public void NavEntries_EqualSortOrder_FallsBackToTheLabel()
    {
        var snapshot = Snapshot(false,
            Module("B", route: "/b", sortOrder: 10, label: "Beta"),
            Module("A", route: "/a", sortOrder: 10, label: "Alpha"));

        Assert.Equal(new[] { "A", "B" }, snapshot.NavEntries().Select(e => e.Key).ToArray());
    }

    [Fact]
    public void NavEntries_SkipDisabledModules()
    {
        var snapshot = Snapshot(false,
            Module("On", route: "/on"),
            Module("Off", enabled: false, route: "/off"));

        Assert.Equal(new[] { "On" }, snapshot.NavEntries().Select(e => e.Key).ToArray());
    }

    [Fact]
    public void NavEntries_SkipModulesWithoutARoute()
    {
        var snapshot = Snapshot(false, Module("Routed", route: "/routed"), Module("Headless"));

        Assert.Equal(new[] { "Routed" }, snapshot.NavEntries().Select(e => e.Key).ToArray());
    }

    [Fact]
    public void NavEntries_SkipModulesWhosePagesDoNotExistYet()
    {
        // switching a module on before its pages ship is allowed; a tab onto a 404 is not
        var snapshot = Snapshot(false,
            Module("Built", route: "/built"),
            Module("Planned", route: "/planned", available: false));

        Assert.Equal(new[] { "Built" }, snapshot.NavEntries().Select(e => e.Key).ToArray());
    }

    [Fact]
    public void NavEntries_AreEmptyUnderTheKillSwitch()
    {
        var snapshot = Snapshot(true, Module("A", route: "/a"), Module("B", route: "/b"));

        Assert.Empty(snapshot.NavEntries());
    }

    [Fact]
    public void HasNavEntry_IsFalseForBlankRoutes()
    {
        Assert.False(Module("A").HasNavEntry);
        Assert.False(Module("B", route: "   ").HasNavEntry);
        Assert.True(Module("C", route: "/c").HasNavEntry);
    }

    [Fact]
    public void Find_ReturnsNullForAnUnknownKey()
        => Assert.Null(Snapshot(false, Module("A")).Find("nope"));
}
