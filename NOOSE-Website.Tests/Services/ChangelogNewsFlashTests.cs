using NOOSE_Website.Models.Changelog;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Services;

/// <summary>How much of the news the update window names before it points at the full page.</summary>
public sealed class ChangelogNewsFlashTests
{
    private static ChangelogReleaseView Release(string version, int lines)
        => new(version, version, new DateTime(2026, 9, 1), $"Fassung {version}", null,
            Enumerable.Range(1, lines)
                .Select(i => new ChangelogEntryView($"{version}-{i}", ChangelogKind.Neu, $"{version} Zeile {i}", null))
                .ToList());

    private static ChangelogNewsFlash Flash(params ChangelogReleaseView[] releases)
        => new(releases.Sum(r => r.Entries.Count), releases.FirstOrDefault()?.Version, releases);

    [Fact]
    public void The_preview_fills_up_across_releases_in_reading_order()
    {
        var flash = Flash(Release("2.3.00", 3), Release("2.2.00", 4));

        var preview = flash.Preview(5);

        Assert.Equal(["2.3.00", "2.2.00"], preview.Select(r => r.Version));
        Assert.Equal(3, preview[0].Entries.Count);
        Assert.Equal(["2.2.00 Zeile 1", "2.2.00 Zeile 2"], preview[1].Entries.Select(e => e.Title));
        Assert.Equal(2, flash.Hidden(5));
    }

    [Fact]
    public void A_release_past_the_cap_is_left_out_whole()
    {
        var flash = Flash(Release("2.3.00", 5), Release("2.2.00", 1));

        var preview = flash.Preview(5);

        Assert.Equal("2.3.00", Assert.Single(preview).Version);
        Assert.Equal(1, flash.Hidden(5));
    }

    [Fact]
    public void Short_news_is_shown_whole()
    {
        var flash = Flash(Release("2.3.00", 2));

        Assert.Equal(2, flash.Preview().Single().Entries.Count);
        Assert.Equal(0, flash.Hidden());
    }

    [Fact]
    public void The_default_cap_is_the_shipped_one()
    {
        var flash = Flash(Release("2.3.00", 9));

        Assert.Equal(ChangelogNewsFlash.PreviewLines, flash.Preview().Sum(r => r.Entries.Count));
        Assert.Equal(9 - ChangelogNewsFlash.PreviewLines, flash.Hidden());
    }

    [Fact]
    public void Nothing_previews_as_nothing()
    {
        Assert.Empty(ChangelogNewsFlash.None.Preview());
        Assert.Equal(0, ChangelogNewsFlash.None.Hidden());
        Assert.Empty(Flash(Release("2.3.00", 3)).Preview(0));
    }

    [Fact]
    public void The_preview_leaves_the_news_itself_untouched()
    {
        var flash = Flash(Release("2.3.00", 7));

        _ = flash.Preview(2);

        Assert.Equal(7, flash.Releases.Single().Entries.Count);
    }
}
