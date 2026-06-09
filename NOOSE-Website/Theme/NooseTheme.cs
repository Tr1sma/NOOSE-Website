using MudBlazor;

namespace NOOSE_Website.Theme;

/// <summary>
/// Dunkles NOOSE-Theme ("Anthrazit + Cyan"). Zentrale Farb- und Layout-Definition der App.
/// Wird in <c>MainLayout</c> an den <see cref="MudThemeProvider"/> gehängt (IsDarkMode = true).
/// In Phase 7 kann das Theme später im Admin (ohne Code) angepasst werden.
/// </summary>
public static class NooseTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            // Akzente
            Primary = "#22D3EE",               // Cyan
            PrimaryContrastText = "#06222A",
            Secondary = "#3FB950",
            Tertiary = "#7C8CF8",

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
