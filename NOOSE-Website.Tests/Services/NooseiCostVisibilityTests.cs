using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services;

/// <summary>Structural guard: real money belongs to the AI owner, everyone else counts in quota tokens.</summary>
/// <remarks>
/// Two layers, because a page-level policy is easy to add and just as easy to forget. First a location rule —
/// money may only be rendered under <c>Components/Pages/Admin/</c>, whose page hangs on
/// <c>Policies.LeadershipPage</c> as a whole. Then an owner rule on top: even inside that folder the amount must
/// be gated on <c>IsAiOwner()</c>, since leadership and other admins run the quota without ever seeing a price.
/// </remarks>
public class NooseiCostVisibilityTests
{
    /// <summary>Anything that puts a currency amount on screen.</summary>
    private static readonly string[] MoneyMarkers = ["ToCents", "CostUsd", "ToCost(", "¢", "Realkosten"];

    /// <summary>The one folder whose every page is behind the leadership policy.</summary>
    private static readonly string AdminPages =
        $"{Path.DirectorySeparatorChar}Components{Path.DirectorySeparatorChar}Pages{Path.DirectorySeparatorChar}Admin{Path.DirectorySeparatorChar}";

    private static string ComponentRoot([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "NOOSE-Website", "Components"));

    private static IEnumerable<string> Components(string root) => Directory
        .EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static bool RendersMoney(string file)
    {
        var text = File.ReadAllText(file);
        return MoneyMarkers.Any(m => text.Contains(m, StringComparison.Ordinal));
    }

    [Fact]
    public void NoComponentOutsideTheAdminPagesRendersARealPrice()
    {
        var root = ComponentRoot();
        Assert.True(Directory.Exists(root), $"Komponentenordner nicht gefunden: {root}");

        var offenders = Components(root)
            .Where(f => !f.Contains(AdminPages, StringComparison.Ordinal))
            .Where(RendersMoney)
            .Select(f => Path.GetRelativePath(root, f))
            .Order()
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryAdminComponentThatRendersMoneyGatesItOnTheAiOwner()
    {
        var root = ComponentRoot();

        var offenders = Components(root)
            .Where(f => f.Contains(AdminPages, StringComparison.Ordinal))
            .Where(RendersMoney)
            // the dialog receives the flag as a parameter, the panels derive it from IsAiOwner(); both name it
            .Where(f => !File.ReadAllText(f).Contains("MaySeeCost", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(root, f))
            .Order()
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheAgentsOwnQuotaPageTalksOnlyAboutTokens()
    {
        var panel = Path.Combine(ComponentRoot(), "Pages", "Ki", "Shared", "NooseiQuotaPanel.razor");
        Assert.True(File.Exists(panel), $"Datei nicht gefunden: {panel}");

        var text = File.ReadAllText(panel);

        // whole word and case-sensitive: the currency is a German noun, "CarryPercent" is not one
        Assert.DoesNotMatch(new Regex(@"\bCent\b"), text);
        Assert.Contains("Token", text, StringComparison.Ordinal);
    }
}
