using System.Runtime.CompilerServices;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>Which routes count as public — the basis of both the noindex header and robots.txt.</summary>
public class PublicRoutesTests
{
    private static string RobotsPath([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(here)!, "..", "..", "..", "NOOSE-Website", "wwwroot", "robots.txt"));

    [Theory]
    [InlineData("/")]
    [InlineData("/karriere")]
    [InlineData("/gesucht")]
    [InlineData("/gesucht/NOOSE-FA-2026-0001")]
    [InlineData("/gesucht/NOOSE-FA-2026-0001/foto")]
    [InlineData("/gesucht/NOOSE-FA-2026-0001/druck")]
    [InlineData("/gefasst")]
    [InlineData("/GESUCHT")]
    [InlineData("/datenschutz")]
    [InlineData("/nutzungsbedingungen")]
    [InlineData("/info/auftrag")]
    [InlineData("/gefahr/personen")]
    [InlineData("/robots.txt")]
    public void Public_paths_are_indexable(string path)
        => Assert.True(PublicRoutes.IsPublic(path), path);

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/personen")]
    [InlineData("/personen/abc")]
    [InlineData("/fraktionen")]
    [InlineData("/einstellungen")]
    [InlineData("/nachweis")]
    [InlineData("/fahndung")]
    [InlineData("/suche")]
    [InlineData("/Account/Login")]
    [InlineData("/portal")]
    public void Internal_paths_are_not_indexable(string path)
        => Assert.False(PublicRoutes.IsPublic(path), path);

    [Fact]
    public void The_citizens_own_account_area_is_not_indexable()
    {
        // /buerger is private, not public: it shows one citizen their own submissions
        Assert.False(PublicRoutes.IsPublic("/buerger"));
        Assert.False(PublicRoutes.IsPublic("/buerger/profil"));
    }

    [Fact]
    public void Matching_stops_at_a_segment_boundary()
    {
        // /gesucht must not make /gesuchte-liste public
        Assert.False(PublicRoutes.IsPublic("/gesuchte-liste"));
        Assert.False(PublicRoutes.IsPublic("/karriere-intern"));
    }

    [Fact]
    public void Internal_fahndung_stays_internal_next_to_public_gesucht()
    {
        // the two boards share a topic and nothing else; /fahndung is the internal one
        Assert.False(PublicRoutes.IsPublic("/fahndung"));
        Assert.True(PublicRoutes.IsPublic("/gesucht"));
    }

    [Fact]
    public void The_internal_tip_desk_stays_internal_next_to_the_public_form()
    {
        // same trap as /fahndung next to /gesucht, one letter apart: /hinweis is the form, /hinweise is the desk
        Assert.True(PublicRoutes.IsPublic("/hinweis"));
        Assert.False(PublicRoutes.IsPublic("/hinweise"));
        Assert.False(PublicRoutes.IsPublic("/hinweise/abc"));
        Assert.False(PublicRoutes.IsPublic("/buerger/hinweise"));
    }

    [Fact]
    public void Robots_txt_exists_and_disallows_everything_by_default()
    {
        var text = File.ReadAllText(RobotsPath());

        Assert.Contains("User-agent: *", text, StringComparison.Ordinal);
        Assert.Contains("Disallow: /", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_public_prefix_is_covered_by_an_allow_line()
    {
        // robots.txt matches by prefix, so "Allow: /gefahr" covers /gefahr/fraktionen — an exact-name check here
        // would demand a line per sub-route and drift the moment a module adds one
        var lines = AllowLines();

        var missing = PublicRoutes.Prefixes
            .Where(p => p != "/robots.txt")
            .Where(p => !lines.Any(l => p.Equals(l, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(l + "/", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(missing.Length == 0,
            "Öffentlich, aber von keiner Allow-Zeile gedeckt: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_allow_line_really_points_at_a_public_path()
    {
        var wrong = AllowLines()
            .Where(l => l != "/$")
            .Where(l => !PublicRoutes.IsPublic(l))
            .ToArray();

        Assert.True(wrong.Length == 0,
            "In robots.txt erlaubt, gilt aber als intern: " + string.Join(", ", wrong));
    }

    private static IReadOnlyList<string> AllowLines()
        => File.ReadAllLines(RobotsPath())
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))
            .Select(l => l["Allow:".Length..].Trim())
            .ToList();
}
