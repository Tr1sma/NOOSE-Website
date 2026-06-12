namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Schlüssel-Konstanten der <c>SystemEinstellungen</c>-Tabelle (Phase 7).</summary>
public static class SystemEinstellungKeys
{
    public const string WartungsmodusAktiv = "WartungsmodusAktiv";
    public const string WartungsmodusText = "WartungsmodusText";
    public const string BannerText = "BannerText";
    public const string BannerStufe = "BannerStufe";
    public const string ThemePrimary = "ThemePrimary";
    public const string ThemeSecondary = "ThemeSecondary";
    public const string ThemeTertiary = "ThemeTertiary";
    public const string LogoDateiname = "LogoDateiname";
    public const string LogoContentType = "LogoContentType";
}

/// <summary>Banner-Dringlichkeit (als Text gespeichert; UI mappt auf MudBlazor-Severity).</summary>
public static class BannerStufen
{
    public const string Info = "Info";
    public const string Warnung = "Warnung";
    public const string Fehler = "Fehler";

    public static readonly IReadOnlyList<string> Alle = new[] { Info, Warnung, Fehler };
}

/// <summary>
/// Gecachter Lese-Schnappschuss aller Systemeinstellungen (Wartungsmodus, Banner, Theme, Logo).
/// Nicht gesetzte Werte sind <c>null</c> → der Code-Standard gilt (Standard-Theme, kein Banner …).
/// </summary>
public sealed record SystemKonfiguration(
    bool WartungsmodusAktiv,
    string? WartungsmodusText,
    string? BannerText,
    string BannerStufe,
    string? ThemePrimary,
    string? ThemeSecondary,
    string? ThemeTertiary,
    string? LogoDateiname,
    string? LogoContentType)
{
    public bool HatLogo => !string.IsNullOrWhiteSpace(LogoDateiname);
}

/// <summary>Eingabemodell der Admin-Seite „System" (Logo läuft separat über den Upload-Pfad).</summary>
public class SystemKonfigurationEingabe
{
    public bool WartungsmodusAktiv { get; set; }
    public string? WartungsmodusText { get; set; }
    public string? BannerText { get; set; }
    public string BannerStufe { get; set; } = BannerStufen.Info;
    public string? ThemePrimary { get; set; }
    public string? ThemeSecondary { get; set; }
    public string? ThemeTertiary { get; set; }
}
