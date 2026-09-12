using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>What a changelog line did to the site, in the reader's terms.</summary>
/// <remarks>
/// Three values on purpose. A reader wants to know whether something is new, better or repaired; finer distinctions
/// (refactor, security, performance) are author categories and would leak the technology the reader does not care
/// about. Its own enum rather than sharing one: a fourth value wanted here must not arrive at another table unasked.
/// </remarks>
public enum ChangelogKind
{
    Neu = 0,
    Verbessert = 1,
    Behoben = 2,
}

/// <summary>Display labels, colours and icons.</summary>
public static class ChangelogKindDisplay
{
    public static string Name(ChangelogKind kind) => kind switch
    {
        ChangelogKind.Neu => "Neu",
        ChangelogKind.Verbessert => "Verbessert",
        ChangelogKind.Behoben => "Behoben",
        _ => "—",
    };

    public static string Icon(ChangelogKind kind) => kind switch
    {
        ChangelogKind.Neu => Icons.Material.Filled.AutoAwesome,
        ChangelogKind.Verbessert => Icons.Material.Filled.TrendingUp,
        ChangelogKind.Behoben => Icons.Material.Filled.BuildCircle,
        _ => Icons.Material.Filled.Circle,
    };

    public static Color Colour(ChangelogKind kind) => kind switch
    {
        ChangelogKind.Neu => Color.Success,
        ChangelogKind.Verbessert => Color.Info,
        ChangelogKind.Behoben => Color.Warning,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<ChangelogKind> All = new[]
    {
        ChangelogKind.Neu,
        ChangelogKind.Verbessert,
        ChangelogKind.Behoben,
    };
}
