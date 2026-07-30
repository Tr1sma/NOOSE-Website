using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Services;

/// <summary>The static override store behind the display classes (DB-backed enum label overrides).</summary>
[Collection("EnumLabels")]
public class EnumLabelTextTests : IDisposable
{
    public EnumLabelTextTests()
    {
        // static store: start each test from a clean slate
        EnumLabelText.ReplaceAll([]);
    }

    public void Dispose()
    {
        EnumLabelText.ReplaceAll([]);
    }

    [Fact]
    public void Unknown_key_returns_null()
        => Assert.Null(EnumLabelText.Get("Rank", "Director"));

    [Fact]
    public void Stored_override_is_returned()
    {
        EnumLabelText.ReplaceAll([("Rank", "Director", "Chefin")]);
        Assert.Equal("Chefin", EnumLabelText.Get("Rank", "Director"));
    }

    [Fact]
    public void Same_key_in_different_lists_does_not_collide()
    {
        EnumLabelText.ReplaceAll([("Rank", "Misc", "A"), ("RelationType", "Misc", "B")]);
        Assert.Equal("A", EnumLabelText.Get("Rank", "Misc"));
        Assert.Equal("B", EnumLabelText.Get("RelationType", "Misc"));
    }

    [Fact]
    public void ReplaceAll_swaps_previous_entries()
    {
        EnumLabelText.ReplaceAll([("Rank", "Director", "Chefin")]);
        EnumLabelText.ReplaceAll([("Rank", "DeputyDirector", "Stellvertreter")]);
        Assert.Null(EnumLabelText.Get("Rank", "Director"));
        Assert.Equal("Stellvertreter", EnumLabelText.Get("Rank", "DeputyDirector"));
    }
}
