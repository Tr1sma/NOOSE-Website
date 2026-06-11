using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAktualitaetService" />
public class AktualitaetService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache) : IAktualitaetService
{
    private const string CacheKey = "aktualitaet:schwellen";

    /// <summary>
    /// Unterstützte Akten + Standard-Schwellwerte. Personen/Operationen/Taskforces/Vorgänge ändern sich häufiger
    /// (30/90 Tage); Organisationen (Fraktion/Gruppe/Partei) leben länger ohne Update (60/180 Tage).
    /// </summary>
    private static readonly AktualitaetsTypInfo[] Typen =
    {
        new(nameof(Person), "Person", 30, 90),
        new(nameof(Fraktion), "Fraktion", 60, 180),
        new(nameof(Personengruppe), "Personengruppe", 60, 180),
        new(nameof(Partei), "Partei", 60, 180),
        new(nameof(Operation), "Operation", 30, 90),
        new(nameof(Taskforce), "Taskforce", 30, 90),
        new(nameof(Vorgang), "Vorgang", 30, 90),
    };

    public IReadOnlyList<AktualitaetsTypInfo> UnterstuetzteTypen => Typen;

    public async Task<IReadOnlyDictionary<string, (int WarnungTage, int VeraltetTage)>> GetSchwellenAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, (int, int)>? gecacht) && gecacht is not null)
        {
            return gecacht;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var overrides = await db.AktualitaetsSchwellen
            .ToDictionaryAsync(s => s.AktenTyp, s => (s.WarnungTage, s.VeraltetTage), cancellationToken);

        // Standard je Typ, überschrieben durch gespeicherte Werte. Stets ein Eintrag je unterstütztem Typ.
        var ergebnis = Typen.ToDictionary(
            t => t.Typ,
            t => overrides.TryGetValue(t.Typ, out var o) ? o : (t.StandardWarnungTage, t.StandardVeraltetTage));

        cache.Set(CacheKey, (IReadOnlyDictionary<string, (int, int)>)ergebnis, TimeSpan.FromMinutes(10));
        return ergebnis;
    }

    public async Task<(int WarnungTage, int VeraltetTage)> GetSchwelleAsync(string aktenTyp, CancellationToken cancellationToken = default)
    {
        var alle = await GetSchwellenAsync(cancellationToken);
        if (alle.TryGetValue(aktenTyp, out var s))
        {
            return s;
        }
        var standard = Typen.FirstOrDefault(t => t.Typ == aktenTyp);
        return standard is not null ? (standard.StandardWarnungTage, standard.StandardVeraltetTage) : (30, 90);
    }

    public async Task<AktualitaetsStufe> BewertenAsync(string aktenTyp, DateTime referenzdatum, CancellationToken cancellationToken = default)
    {
        var (warnung, veraltet) = await GetSchwelleAsync(aktenTyp, cancellationToken);
        return AktualitaetsBewertung.Stufe(warnung, veraltet, referenzdatum, DateTime.UtcNow);
    }

    public async Task SpeichernAsync(string aktenTyp, int warnungTage, int veraltetTage, ClaimsPrincipal handelnder,
        CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);

        if (Typen.All(t => t.Typ != aktenTyp))
        {
            throw new InvalidOperationException($"Unbekannter Aktentyp '{aktenTyp}'.");
        }
        // Plausibilität: nicht-negativ, und „rot" frühestens ab „gelb".
        warnungTage = Math.Max(0, warnungTage);
        veraltetTage = Math.Max(warnungTage, veraltetTage);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var vorhanden = await db.AktualitaetsSchwellen.FirstOrDefaultAsync(s => s.AktenTyp == aktenTyp, cancellationToken);
        if (vorhanden is null)
        {
            db.AktualitaetsSchwellen.Add(new AktualitaetsSchwelle
            {
                AktenTyp = aktenTyp,
                WarnungTage = warnungTage,
                VeraltetTage = veraltetTage,
            });
        }
        else
        {
            vorhanden.WarnungTage = warnungTage;
            vorhanden.VeraltetTage = veraltetTage;
        }
        await db.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKey);
    }
}
