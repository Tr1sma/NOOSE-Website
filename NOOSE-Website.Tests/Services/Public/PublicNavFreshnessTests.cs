using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>Guards the nav bar's re-render subscription: the shell instance survives client-side
/// navigation, so without <c>LocationChanged</c> the active tab freezes on the first render.</summary>
public class PublicNavFreshnessTests
{
    private static string Root([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "NOOSE-Website", "Components"));

    private static string Code()
    {
        var file = Path.Combine(Root(), "Layout", "PublicNav.razor");
        Assert.True(File.Exists(file), $"Öffentliche Tab-Leiste nicht gefunden: {file}");
        // comments may name the event while the subscription itself is gone
        return Regex.Replace(File.ReadAllText(file), @"@\*.*?\*@", " ", RegexOptions.Singleline);
    }

    [Fact]
    public void PublicNavRerendersOnNavigation()
    {
        var code = Code();
        Assert.Contains("Nav.LocationChanged +=", code);
        Assert.Contains("@implements IDisposable", code);
    }
}
