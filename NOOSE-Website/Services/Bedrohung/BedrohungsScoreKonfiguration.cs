using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Laufzeit-Konfiguration des Bedrohungs-Score-Algorithmus „EHK-Score" (Phase 8/Block D). Ersetzt die früheren
/// festen <c>const</c>-Werte: alle hier abgelegten Zahlen sind admin-einstellbar (Admin-Seite /admin/bedrohungs-score,
/// persistiert als JSON in <c>BedrohungsScoreKonfig</c>). Die <b>Default</b>-Instanz (<c>new()</c>) entspricht
/// bit-genau den bisherigen Konstanten → bei unveränderter Konfiguration liefert der Score identische Ergebnisse.
///
/// <para>NICHT hier (bewusst fix, nie editierbar): die Einstufungs-Sockel und die GefährdungsStufe-Schwellen
/// (25/50/75) – sie sind die semantischen Anker (<see cref="BedrohungsScoreKonstanten.Sockel"/> /
/// <c>GefaehrdungsStufeLogic</c>). Die Schwere-Keyword-Tabellen bleiben ebenfalls fix (nur ihre Zahlen sind hier).</para>
///
/// <para>Vorwärtskompatibel: fehlt ein Feld im gespeicherten JSON (älterer Stand), greift der Initializer-Default –
/// deshalb hat jedes Property einen Default-Wert.</para>
/// </summary>
public sealed class BedrohungsScoreKonfiguration
{
    // ---- Geteilt (wirkt auf Fraktion UND Person) ----
    public double HalbwertszeitTage { get; set; } = 90.0;
    public int KonfidenzFrischeTage { get; set; } = 180;
    public int TriageSchwelle { get; set; } = 50;
    public double ArtGewichtSchwer { get; set; } = 3.0;
    public double ArtGewichtMittel { get; set; } = 2.0;
    public double ArtGewichtLeicht { get; set; } = 1.0;
    public double AusgangErschossen { get; set; } = 2.0;
    public double AusgangSpritze { get; set; } = 1.5;
    public double AusgangLaeuftNoch { get; set; } = 1.2;
    public double AusgangEntlassen { get; set; } = 1.0;

    // ---- Fraktion S1 (Aktivitäts-/Maßnahmen-Heat) ----
    public double CapS1 { get; set; } = 55.0;
    public double S1Nenner { get; set; } = 6.0;
    public double DokHeatGewicht { get; set; } = 0.6;
    public double ProMitgliedDokCap { get; set; } = 8.0;

    // ---- Fraktion S2 (Organisation & Reichweite); Sub-Caps summieren auf CapS2 ----
    public double CapS2 { get; set; } = 22.0;
    public double CapGroesse { get; set; } = 10.0;
    public double GroesseNenner { get; set; } = 15.0;
    public int RaengeMaxPunkte { get; set; } = 3;
    public double LeitungPunkte { get; set; } = 2.0;
    public double AnwesenPunkte { get; set; } = 1.0;
    public double CapWaffen { get; set; } = 3.0;
    public double WaffenNenner { get; set; } = 3.0;
    public double CapInfra { get; set; } = 3.0;
    public double InfraNenner { get; set; } = 4.0;
    public double DrogenrouteGewicht { get; set; } = 2.0;

    // ---- Fraktion S3 (Konflikt & Bündnis) ----
    public double CapS3 { get; set; } = 15.0;
    public double S3Nenner { get; set; } = 4.0;
    public double KonfliktGewicht { get; set; } = 2.0;
    public double BuendnisGewicht { get; set; } = 1.0;

    // ---- Fraktion S4 (Netzwerk-Zentralität) ----
    public double CapS4 { get; set; } = 8.0;
    public double S4Nenner { get; set; } = 4.0;

    // ---- Person P1 (Maßnahmen-Heat) ----
    public double CapP1 { get; set; } = 40.0;
    public double P1Nenner { get; set; } = 4.0;

    // ---- Person P2 (Bewaffnung & Eskalation); Sub-Caps summieren auf CapP2 ----
    public double CapP2 { get; set; } = 22.0;
    public double PersonCapWaffen { get; set; } = 14.0;
    public double PersonWaffenNenner { get; set; } = 2.0;
    public double FluechtigPunkte { get; set; } = 8.0;

    // ---- Person P3 (Observations-Heat) ----
    public double CapP3 { get; set; } = 18.0;
    public double P3Nenner { get; set; } = 3.0;
    /// <summary>Gewicht einer abgeschlossenen Observation relativ zu einer laufenden (1.0). Beide zeit-abklingend.</summary>
    public double ObservationAbgeschlossenGewicht { get; set; } = 0.6;

    // ---- Person P4 (Soziale Gefahr: Beziehungen + Leitungsrollen) ----
    public double CapP4 { get; set; } = 12.0;
    public double P4Nenner { get; set; } = 4.0;
    public double FeindGewicht { get; set; } = 2.0;
    public double VerbuendeterGewicht { get; set; } = 1.0;
    public double GpGewicht { get; set; } = 1.0;
    public double LeitungGewicht { get; set; } = 1.5;

    // ---- Person P5 (Netzwerk-Zentralität) ----
    public double CapP5 { get; set; } = 8.0;
    public double P5Nenner { get; set; } = 4.0;

    /// <summary>Eine frische Default-Instanz (= bisherige hartkodierte Werte).</summary>
    public static BedrohungsScoreKonfiguration Default() => new();

    // ---- Schwere-Keyword-Tabellen (FIX, nicht konfigurierbar – nur die Zahlen oben sind es) ----
    private static readonly string[] ArtSchwer =
        { "mord", "tötung", "toetung", "hinrichtung", "geiselnahme", "entführung", "entfuehrung", "anschlag", "terror" };
    private static readonly string[] ArtMittel =
        { "raub", "überfall", "ueberfall", "schießerei", "schiesserei", "bank", "erpressung", "schutzgeld", "waffenhandel", "drogenhandel" };

    /// <summary>Schweregewicht einer Aktivitäts-Art: schwer/mittel/sonst (NIE 0 – eine erfasste Tat ist immer Signal).</summary>
    public double ArtGewicht(string? art)
    {
        if (string.IsNullOrWhiteSpace(art))
        {
            return ArtGewichtLeicht;
        }
        var a = art.ToLowerInvariant();
        if (ArtSchwer.Any(k => a.Contains(k)))
        {
            return ArtGewichtSchwer;
        }
        if (ArtMittel.Any(k => a.Contains(k)))
        {
            return ArtGewichtMittel;
        }
        return ArtGewichtLeicht;
    }

    /// <summary>Gewicht eines Maßnahmen-Ausgangs.</summary>
    public double AusgangGewicht(MassnahmeAusgang ausgang) => ausgang switch
    {
        MassnahmeAusgang.Erschossen => AusgangErschossen,
        MassnahmeAusgang.Spritze => AusgangSpritze,
        MassnahmeAusgang.LaeuftNoch => AusgangLaeuftNoch,
        _ => AusgangEntlassen, // OffiziellEntlassen
    };
}
