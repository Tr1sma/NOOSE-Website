namespace NOOSE_Website.Models.Common;

/// <summary>Schlüssel-Konstanten der <c>SystemEinstellungen</c>-Tabelle (Phase 7).</summary>
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
}

/// <summary>Banner-Dringlichkeit (als Text gespeichert; UI mappt auf MudBlazor-Severity).</summary>
public static class BannerLevels
{
    public const string Info = "Info";
    public const string Warning = "Warnung";
    public const string Error = "Fehler";

    public static readonly IReadOnlyList<string> All = new[] { Info, Warning, Error };
}

/// <summary>
/// Gecachter Lese-Schnappschuss aller Systemeinstellungen (Wartungsmodus, Banner, Theme, Logo).
/// Nicht gesetzte Werte sind <c>null</c> → der Code-Standard gilt (Standard-Theme, kein Banner …).
/// </summary>
public sealed record SystemConfiguration(
    bool MaintenanceModeActive,
    string? MaintenanceModeText,
    string? BannerText,
    string BannerLevel,
    string? ThemePrimary,
    string? ThemeSecondary,
    string? ThemeTertiary,
    string? LogoFileName,
    string? LogoContentType)
{
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoFileName);
}

/// <summary>Eingabemodell der Admin-Seite „System" (Logo läuft separat über den Upload-Pfad).</summary>
public class SystemConfigurationInput
{
    public bool MaintenanceModeActive { get; set; }
    public string? MaintenanceModeText { get; set; }
    public string? BannerText { get; set; }
    public string BannerLevel { get; set; } = BannerLevels.Info;
    public string? ThemePrimary { get; set; }
    public string? ThemeSecondary { get; set; }
    public string? ThemeTertiary { get; set; }
}
