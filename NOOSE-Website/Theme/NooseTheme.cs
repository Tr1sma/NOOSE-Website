using MudBlazor;

namespace NOOSE_Website.Theme;

/// <summary>
/// Dunkles NOOSE-Theme ("Anthrazit + Cyan"). Zentrale Farb- und Layout-Definition der App.
/// Wird in <c>MainLayout</c> an den <see cref="MudThemeProvider"/> gehängt (IsDarkMode = true).
/// Die Akzentfarben können seit Phase 7 im Admin (/admin/system) ohne Code überschrieben
/// werden – siehe <see cref="MitFarben"/>.
/// </summary>
public static class NooseTheme
{
    /// <summary>Standard-Akzentfarben (Fallback, wenn im Admin nichts gesetzt ist).</summary>
    public const string DefaultPrimary = "#22D3EE";
    public const string DefaultSecondary = "#3FB950";
    public const string DefaultTertiary = "#7C8CF8";

    /// <summary>
    /// Baut das Theme mit optional überschriebenen Akzentfarben (Admin-Theming, Phase 7).
    /// <c>null</c>/leer → Standardfarbe. Liefert eine frische Instanz, damit per-Circuit-Themes
    /// das statische <see cref="Theme"/> nicht verändern.
    /// </summary>
    public static MudTheme WithColours(string? primary, string? secondary, string? tertiary)
    {
        var theme = Generate();
        theme.PaletteDark.Primary = string.IsNullOrWhiteSpace(primary) ? DefaultPrimary : primary;
        theme.PaletteDark.Secondary = string.IsNullOrWhiteSpace(secondary) ? DefaultSecondary : secondary;
        theme.PaletteDark.Tertiary = string.IsNullOrWhiteSpace(tertiary) ? DefaultTertiary : tertiary;
        return theme;
    }

    public static readonly MudTheme Theme = Generate();

    private static MudTheme Generate() => new()
    {
        PaletteDark = new PaletteDark
        {
            // Akzente
            Primary = DefaultPrimary,         // Cyan
            PrimaryContrastText = "#06222A",
            Secondary = DefaultSecondary,
            Tertiary = DefaultTertiary,

            // Status
            Info = "#22D3EE",
            Success = "#3FB950",               // gruen
            Warning = "#D29922",               // gelb/amber
            Error = "#F85149",                 // rot
            Dark = "#0A0D12",

            // Flaechen
            Background = "#0E1116",
            BackgroundGray = "#0B0E13",
            Surface = "#161B22",

            // Topbar
            AppbarBackground = "#11161D",
            AppbarText = "#E6EDF3",

            // Seitennavigation
            DrawerBackground = "#0F141A",
            DrawerText = "#C2CBD6",
            DrawerIcon = "#8B98A8",

            // Text
            TextPrimary = "#E6EDF3",
            TextSecondary = "#9BA8B8",
            TextDisabled = "#5A6677",

            // Aktionen / Linien
            ActionDefault = "#8B98A8",
            ActionDisabled = "#44505F",
            ActionDisabledBackground = "#1B222B",
            Divider = "#222B36",
            DividerLight = "#1A222B",
            LinesDefault = "#222B36",
            LinesInputs = "#2B3744",
            TableLines = "#1E2731",
            TableStriped = "#12171E",
            TableHover = "#1A212B",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            AppbarHeight = "60px",
        },
    };
}
