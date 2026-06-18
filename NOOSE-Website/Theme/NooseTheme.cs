using MudBlazor;

namespace NOOSE_Website.Theme;

/// <summary>Dark NOOSE theme; central colour/layout definition. Dark mode only; accent colours overridable in admin.</summary>
public static class NooseTheme
{
    /// <summary>Default accent colours (fallback when nothing is set in admin).</summary>
    public const string DefaultPrimary = "#22D3EE";
    public const string DefaultSecondary = "#3FB950";
    public const string DefaultTertiary = "#7C8CF8";

    /// <summary>Builds the theme with optionally overridden accent colours; returns a fresh instance so per-circuit themes don't mutate the static one.</summary>
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
            Primary = DefaultPrimary,
            PrimaryContrastText = "#06222A",
            Secondary = DefaultSecondary,
            Tertiary = DefaultTertiary,

            Info = "#22D3EE",
            Success = "#3FB950",
            Warning = "#D29922",
            Error = "#F85149",
            Dark = "#0A0D12",

            Background = "#0E1116",
            BackgroundGray = "#0B0E13",
            Surface = "#161B22",

            AppbarBackground = "#11161D",
            AppbarText = "#E6EDF3",

            DrawerBackground = "#0F141A",
            DrawerText = "#C2CBD6",
            DrawerIcon = "#8B98A8",

            TextPrimary = "#E6EDF3",
            TextSecondary = "#9BA8B8",
            TextDisabled = "#5A6677",

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
