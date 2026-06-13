using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBedrohungsScoreKonfigService" />
public class BedrohungsScoreKonfigService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache) : IBedrohungsScoreKonfigService
{
    private const string CacheKey = "bedrohung:konfig";

    public async Task<BedrohungsScoreKonfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out BedrohungsScoreKonfiguration? gecacht) && gecacht is not null)
        {
            return gecacht;
        }
        var konfig = await LadeAsync(cancellationToken);
        cache.Set(CacheKey, konfig, TimeSpan.FromMinutes(10));
        return konfig;
    }

    public Task<BedrohungsScoreKonfiguration> GetBearbeitbarAsync(CancellationToken cancellationToken = default)
        => LadeAsync(cancellationToken); // immer frische Instanz (nicht gecacht), damit die UI gefahrlos editieren kann

    private async Task<BedrohungsScoreKonfiguration> LadeAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var json = (await db.BedrohungsScoreKonfigs
            .FirstOrDefaultAsync(k => k.Id == BedrohungsScoreKonfig.GlobalId, cancellationToken))?.Json;
        if (string.IsNullOrWhiteSpace(json))
        {
            return BedrohungsScoreKonfiguration.Default();
        }
        try
        {
            // Fehlende Felder (älterer Stand) fallen auf die Initializer-Defaults zurück → vorwärtskompatibel.
            return JsonSerializer.Deserialize<BedrohungsScoreKonfiguration>(json, BedrohungsScoreService.JsonOptions)
                   ?? BedrohungsScoreKonfiguration.Default();
        }
        catch (JsonException)
        {
            return BedrohungsScoreKonfiguration.Default();
        }
    }

    public async Task SpeichernAsync(BedrohungsScoreKonfiguration konfig, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default)
    {
        Berechtigung.VerlangeFuehrung(handelnder);
        Validiere(konfig);

        var json = JsonSerializer.Serialize(konfig, BedrohungsScoreService.JsonOptions);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var zeile = await db.BedrohungsScoreKonfigs.FirstOrDefaultAsync(k => k.Id == BedrohungsScoreKonfig.GlobalId, cancellationToken);
        if (zeile is null)
        {
            db.BedrohungsScoreKonfigs.Add(new BedrohungsScoreKonfig { Id = BedrohungsScoreKonfig.GlobalId, Json = json });
        }
        else
        {
            zeile.Json = json;
        }
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    /// <summary>
    /// Erzwingt die Invarianten, an die der Algorithmus gebunden ist: Caps summieren je Subjekt auf 100,
    /// Sub-Caps stimmen mit der Code-Struktur überein, Nenner &gt; 0, Schwere-Tiers monoton und nie 0.
    /// </summary>
    public static void Validiere(BedrohungsScoreKonfiguration k)
    {
        const double tol = 1e-9;

        void Positiv(double wert, string name)
        {
            if (wert <= 0) throw new InvalidOperationException($"{name} muss größer als 0 sein.");
        }
        void NichtNegativ(double wert, string name)
        {
            if (wert < 0) throw new InvalidOperationException($"{name} darf nicht negativ sein.");
        }
        void SummeGleich(double summe, double soll, string was)
        {
            if (Math.Abs(summe - soll) > tol)
                throw new InvalidOperationException($"{was} müssen auf {soll:0.##} summieren (aktuell {summe:0.##}).");
        }

        Positiv(k.HalbwertszeitTage, "Halbwertszeit (Tage)");
        Positiv(k.S1Nenner, "S1-Nenner");
        Positiv(k.GroesseNenner, "Größen-Nenner");
        Positiv(k.WaffenNenner, "Waffen-Nenner");
        Positiv(k.InfraNenner, "Infrastruktur-Nenner");
        Positiv(k.S3Nenner, "S3-Nenner");
        Positiv(k.S4Nenner, "S4-Nenner");
        Positiv(k.P1Nenner, "P1-Nenner");
        Positiv(k.PersonWaffenNenner, "Person-Waffen-Nenner");
        Positiv(k.P3Nenner, "P3-Nenner");
        Positiv(k.P4Nenner, "P4-Nenner");
        Positiv(k.P5Nenner, "P5-Nenner");

        if (k.KonfidenzFrischeTage <= 0) throw new InvalidOperationException("Konfidenz-Frische (Tage) muss größer als 0 sein.");
        if (k.TriageSchwelle is < 0 or > 100) throw new InvalidOperationException("Triage-Schwelle muss zwischen 0 und 100 liegen.");
        if (k.RaengeMaxPunkte < 0) throw new InvalidOperationException("Ränge-Max-Punkte darf nicht negativ sein.");

        // Schwere-Tiers: monoton fallend und nie unter 1 (eine erfasste Tat ist immer Signal).
        if (!(k.ArtGewichtSchwer >= k.ArtGewichtMittel && k.ArtGewichtMittel >= k.ArtGewichtLeicht && k.ArtGewichtLeicht >= 1))
            throw new InvalidOperationException("Schwere-Gewichte müssen erfüllen: schwer ≥ mittel ≥ leicht ≥ 1.");
        NichtNegativ(k.AusgangErschossen, "Ausgang Erschossen");
        NichtNegativ(k.AusgangSpritze, "Ausgang Spritze");
        NichtNegativ(k.AusgangLaeuftNoch, "Ausgang läuft noch");
        NichtNegativ(k.AusgangEntlassen, "Ausgang entlassen");

        // Caps & Gewichte nicht negativ.
        foreach (var (wert, name) in new (double, string)[]
        {
            (k.CapS1, "Cap S1"), (k.CapS2, "Cap S2"), (k.CapS3, "Cap S3"), (k.CapS4, "Cap S4"),
            (k.CapGroesse, "Cap Größe"), (k.LeitungPunkte, "Leitung-Punkte"), (k.AnwesenPunkte, "Anwesen-Punkte"),
            (k.CapWaffen, "Cap Waffen"), (k.CapInfra, "Cap Infrastruktur"), (k.DrogenrouteGewicht, "Drogenroute-Gewicht"),
            (k.KonfliktGewicht, "Konflikt-Gewicht"), (k.BuendnisGewicht, "Bündnis-Gewicht"),
            (k.DokHeatGewicht, "Dok-Heat-Gewicht"), (k.ProMitgliedDokCap, "Pro-Mitglied-Dok-Cap"),
            (k.CapP1, "Cap P1"), (k.CapP2, "Cap P2"), (k.CapP3, "Cap P3"), (k.CapP4, "Cap P4"), (k.CapP5, "Cap P5"),
            (k.PersonCapWaffen, "Person-Cap-Waffen"), (k.FluechtigPunkte, "Flüchtig-Punkte"),
            (k.ObservationAbgeschlossenGewicht, "Observation-abgeschlossen-Gewicht"),
            (k.FeindGewicht, "Feind-Gewicht"), (k.VerbuendeterGewicht, "Verbündeter-Gewicht"),
            (k.GpGewicht, "Geschäftspartner-Gewicht"), (k.LeitungGewicht, "Leitungsrollen-Gewicht"),
        })
        {
            NichtNegativ(wert, name);
        }

        // Caps summieren je Subjekt auf 100 (sonst kann der Score nicht 100 erreichen / Reskalierung wäre nötig).
        SummeGleich(k.CapS1 + k.CapS2 + k.CapS3 + k.CapS4, 100, "Die Fraktion-Caps (S1–S4)");
        SummeGleich(k.CapP1 + k.CapP2 + k.CapP3 + k.CapP4 + k.CapP5, 100, "Die Person-Caps (P1–P5)");
        // S2-Sub-Caps exakt an die Code-Struktur (strukturPkt = Ränge-Max + Leitung + Anwesen).
        SummeGleich(k.CapGroesse + (k.RaengeMaxPunkte + k.LeitungPunkte + k.AnwesenPunkte) + k.CapWaffen + k.CapInfra,
            k.CapS2, "Die S2-Sub-Caps (Größe + Struktur + Waffen + Infrastruktur)");
        // P2-Sub-Caps (Waffen + Flüchtig).
        SummeGleich(k.PersonCapWaffen + k.FluechtigPunkte, k.CapP2, "Die P2-Sub-Caps (Waffen + Flüchtig)");
    }
}
