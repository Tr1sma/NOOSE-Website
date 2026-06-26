namespace NOOSE_Website.Models.Common;

/// <summary>Key constants for the system-settings table.</summary>
public static class SystemSettingKeys
{
    public const string MaintenanceModeActive = "WartungsmodusAktiv";
    public const string MaintenanceModeText = "WartungsmodusText";
    public const string BannerText = "BannerText";
    public const string BannerLevel = "BannerStufe";
    public const string ThemePrimary = "ThemePrimary";
    public const string ThemeSecondary = "ThemeSecondary";
    public const string ThemeTertiary = "ThemeTertiary";
    public const string LogoFileName = "LogoDateiname";
    public const string LogoContentType = "LogoContentType";
    public const string DemoModeActive = "DemoModusAktiv";
}

/// <summary>Banner urgency, stored as text; UI maps it to MudBlazor severity.</summary>
public static class BannerLevels
{
    public const string Info = "Info";
    public const string Warning = "Warnung";
    public const string Error = "Fehler";

    public static readonly IReadOnlyList<string> All = new[] { Info, Warning, Error };
}

/// <summary>Cached read snapshot of all system settings; null values fall back to code defaults.</summary>
public sealed record SystemConfiguration(
    bool MaintenanceModeActive,
    string? MaintenanceModeText,
    string? BannerText,
    string BannerLevel,
    string? ThemePrimary,
    string? ThemeSecondary,
    string? ThemeTertiary,
    string? LogoFileName,
    string? LogoContentType,
    bool DemoModeActive)
{
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoFileName);
}

/// <summary>Input model for the admin System page; logo goes through the separate upload path.</summary>
public class SystemConfigurationInput
{
    public bool MaintenanceModeActive { get; set; }
    public string? MaintenanceModeText { get; set; }
    public string? BannerText { get; set; }
    public string BannerLevel { get; set; } = BannerLevels.Info;
    public string? ThemePrimary { get; set; }
    public string? ThemeSecondary { get; set; }
    public string? ThemeTertiary { get; set; }
    public bool DemoModeActive { get; set; }
}
