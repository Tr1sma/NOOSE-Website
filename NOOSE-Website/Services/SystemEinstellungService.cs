using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISystemEinstellungService" />
public partial class SystemEinstellungService(
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache,
    IWebHostEnvironment env,
    IOptions<FileUploadOptions> uploadOptions) : ISystemEinstellungService
{
    private const string CacheKey = "SystemKonfiguration";
    private static readonly TimeSpan CacheDauer = TimeSpan.FromSeconds(10);

    // Logo liegt – wie alle Uploads – außerhalb von wwwroot und wird über /system/logo ausgeliefert.
    private string LogoBasisPfad => Path.Combine(env.ContentRootPath, "App_Data", "uploads", "system");

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexFarbeRegex();

    public async Task<SystemKonfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out SystemKonfiguration? konfiguration) && konfiguration is not null)
        {
            return konfiguration;
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var werte = await db.SystemEinstellungen
                .ToDictionaryAsync(e => e.Schluessel, e => e.Wert, cancellationToken);

            konfiguration = new SystemKonfiguration(
                WartungsmodusAktiv: string.Equals(werte.GetValueOrDefault(SystemEinstellungKeys.WartungsmodusAktiv), "true", StringComparison.OrdinalIgnoreCase),
                WartungsmodusText: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.WartungsmodusText)),
                BannerText: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.BannerText)),
                BannerStufe: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.BannerStufe)) ?? BannerStufen.Info,
                ThemePrimary: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.ThemePrimary)),
                ThemeSecondary: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.ThemeSecondary)),
                ThemeTertiary: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.ThemeTertiary)),
                LogoDateiname: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.LogoDateiname)),
                LogoContentType: Leer(werte.GetValueOrDefault(SystemEinstellungKeys.LogoContentType)));
        }
        catch (Exception)
        {
            // Die Konfiguration wird in jedem Layout-Render gebraucht – ein DB-Schluckauf darf die App
            // nicht reißen. Standardwerte liefern und NICHT cachen (nächster Aufruf probiert es erneut).
            return Standard();
        }

        cache.Set(CacheKey, konfiguration, CacheDauer);
        return konfiguration;
    }

    public async Task SpeichernAsync(SystemKonfigurationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeAdmin(handelnder);

        foreach (var farbe in new[] { eingabe.ThemePrimary, eingabe.ThemeSecondary, eingabe.ThemeTertiary })
        {
            if (!string.IsNullOrWhiteSpace(farbe) && !HexFarbeRegex().IsMatch(farbe.Trim()))
            {
                throw new InvalidOperationException($"„{farbe}“ ist keine gültige Farbe – bitte als Hex-Wert angeben (z. B. #22D3EE).");
            }
        }
        if (!BannerStufen.Alle.Contains(eingabe.BannerStufe))
        {
            eingabe.BannerStufe = BannerStufen.Info;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.WartungsmodusAktiv, eingabe.WartungsmodusAktiv ? "true" : "false", cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.WartungsmodusText, Leer(eingabe.WartungsmodusText), cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.BannerText, Leer(eingabe.BannerText), cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.BannerStufe, eingabe.BannerStufe, cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.ThemePrimary, Leer(eingabe.ThemePrimary)?.Trim(), cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.ThemeSecondary, Leer(eingabe.ThemeSecondary)?.Trim(), cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.ThemeTertiary, Leer(eingabe.ThemeTertiary)?.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKey);
    }

    public async Task LogoSetzenAsync(Stream inhalt, string originalName, string contentType, long groesseBytes, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeAdmin(handelnder);

        var options = uploadOptions.Value;
        if (!options.ErlaubteContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Nur Bilddateien (JPG, PNG, WebP, GIF) sind als Logo erlaubt.");
        }
        if (groesseBytes > options.MaxBytes)
        {
            throw new InvalidOperationException($"Das Logo ist zu groß (max. {options.MaxBytes / (1024 * 1024)} MB).");
        }

        // Neuer, zufälliger Dateiname je Upload → Browser-Caches des alten Logos laufen ins Leere.
        var endung = Path.GetExtension(originalName);
        if (string.IsNullOrEmpty(endung) || endung.Length > 12 || endung.Skip(1).Any(c => !char.IsLetterOrDigit(c)))
        {
            endung = ".bin";
        }
        var dateiname = $"logo-{Guid.NewGuid():N}{endung.ToLowerInvariant()}";

        Directory.CreateDirectory(LogoBasisPfad);
        var ziel = Path.Combine(LogoBasisPfad, dateiname);
        await using (var fs = File.Create(ziel))
        {
            await inhalt.CopyToAsync(fs, cancellationToken);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var altes = await WertAsync(db, SystemEinstellungKeys.LogoDateiname, cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.LogoDateiname, dateiname, cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.LogoContentType, contentType, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        AlteLogoDateiLoeschen(altes);
        cache.Remove(CacheKey);
    }

    public async Task LogoEntfernenAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeAdmin(handelnder);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var altes = await WertAsync(db, SystemEinstellungKeys.LogoDateiname, cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.LogoDateiname, null, cancellationToken);
        await SetzenAsync(db, SystemEinstellungKeys.LogoContentType, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        AlteLogoDateiLoeschen(altes);
        cache.Remove(CacheKey);
    }

    public async Task<(Stream Inhalt, string ContentType)?> LogoOeffnenAsync(CancellationToken cancellationToken = default)
    {
        var konfiguration = await GetAsync(cancellationToken);
        if (!konfiguration.HatLogo)
        {
            return null;
        }
        var pfad = DateiPfadHelfer.SichererPfad(LogoBasisPfad, konfiguration.LogoDateiname!);
        if (!File.Exists(pfad))
        {
            return null;
        }
        return (File.OpenRead(pfad), konfiguration.LogoContentType ?? "application/octet-stream");
    }

    private static SystemKonfiguration Standard()
        => new(false, null, null, BannerStufen.Info, null, null, null, null, null);

    private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert;

    private static async Task<string?> WertAsync(AppDbContext db, string schluessel, CancellationToken cancellationToken)
        => (await db.SystemEinstellungen.FirstOrDefaultAsync(e => e.Schluessel == schluessel, cancellationToken))?.Wert;

    private static async Task SetzenAsync(AppDbContext db, string schluessel, string? wert, CancellationToken cancellationToken)
    {
        var zeile = await db.SystemEinstellungen.FirstOrDefaultAsync(e => e.Schluessel == schluessel, cancellationToken);
        if (zeile is null)
        {
            db.SystemEinstellungen.Add(new SystemEinstellung { Schluessel = schluessel, Wert = wert });
        }
        else
        {
            zeile.Wert = wert;
        }
    }

    private void AlteLogoDateiLoeschen(string? dateiname)
    {
        if (string.IsNullOrWhiteSpace(dateiname))
        {
            return;
        }
        try
        {
            var pfad = DateiPfadHelfer.SichererPfad(LogoBasisPfad, dateiname);
            if (File.Exists(pfad))
            {
                File.Delete(pfad);
            }
        }
        catch (IOException)
        {
            // Aufräum-Fehler sind unkritisch (verwaiste Datei bleibt liegen).
        }
    }
}
