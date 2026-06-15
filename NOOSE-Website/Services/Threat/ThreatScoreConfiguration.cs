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
public sealed class ThreatScoreConfiguration
{
    // ---- Geteilt (wirkt auf Fraktion UND Person) ----
    public double HalfLifeDays { get; set; } = 90.0;
    public int ConfidenceFreshDays { get; set; } = 180;
    public int TriageThreshold { get; set; } = 50;
    public double KindWeightHeavy { get; set; } = 3.0;
    public double KindWeightMedium { get; set; } = 2.0;
    public double KindWeightLight { get; set; } = 1.0;
    public double OutcomeShot { get; set; } = 2.0;
    public double OutcomeInjection { get; set; } = 1.5;
    public double OutcomeRunningStill { get; set; } = 1.2;
    public double OutcomeReleased { get; set; } = 1.0;

    // ---- Fraktion S1 (Aktivitäts-/Maßnahmen-Heat) ----
    public double CapS1 { get; set; } = 55.0;
    public double S1Denominator { get; set; } = 6.0;
    public double DocHeatWeight { get; set; } = 0.6;
    public double PerMemberDocCap { get; set; } = 8.0;

    // ---- Fraktion S2 (Organisation & Reichweite); Sub-Caps summieren auf CapS2 ----
    public double CapS2 { get; set; } = 22.0;
    public double CapSize { get; set; } = 10.0;
    public double SizeDenominator { get; set; } = 15.0;
    public int RanksMaxPoints { get; set; } = 3;
    public double LeadPoints { get; set; } = 2.0;
    public double EstatePoints { get; set; } = 1.0;
    public double CapWeapons { get; set; } = 3.0;
    public double WeaponsDenominator { get; set; } = 3.0;
    public double CapInfra { get; set; } = 3.0;
    public double InfraDenominator { get; set; } = 4.0;
    public double DrugRouteWeight { get; set; } = 2.0;

    // ---- Fraktion S3 (Konflikt & Bündnis) ----
    public double CapS3 { get; set; } = 15.0;
    public double S3Denominator { get; set; } = 4.0;
    public double ConflictWeight { get; set; } = 2.0;
    public double AllianceWeight { get; set; } = 1.0;

    // ---- Fraktion S4 (Netzwerk-Zentralität) ----
    public double CapS4 { get; set; } = 8.0;
    public double S4Denominator { get; set; } = 4.0;

    // ---- Person P1 (Maßnahmen-Heat) ----
    public double CapP1 { get; set; } = 40.0;
    public double P1Denominator { get; set; } = 4.0;

    // ---- Person P2 (Bewaffnung & Eskalation); Sub-Caps summieren auf CapP2 ----
    public double CapP2 { get; set; } = 22.0;
    public double PersonCapWeapons { get; set; } = 14.0;
    public double PersonWeaponsDenominator { get; set; } = 2.0;
    public double FugitivePoints { get; set; } = 8.0;

    // ---- Person P3 (Observations-Heat) ----
    public double CapP3 { get; set; } = 18.0;
    public double P3Denominator { get; set; } = 3.0;
    /// <summary>Gewicht einer abgeschlossenen Observation relativ zu einer laufenden (1.0). Beide zeit-abklingend.</summary>
    public double ObservationCompletedWeight { get; set; } = 0.6;

    // ---- Person P4 (Soziale Gefahr: Beziehungen + Leitungsrollen) ----
    public double CapP4 { get; set; } = 12.0;
    public double P4Denominator { get; set; } = 4.0;
    public double EnemyWeight { get; set; } = 2.0;
    public double AllyWeight { get; set; } = 1.0;
    public double GpWeight { get; set; } = 1.0;
    public double LeadWeight { get; set; } = 1.5;

    // ---- Person P5 (Netzwerk-Zentralität) ----
    public double CapP5 { get; set; } = 8.0;
    public double P5Denominator { get; set; } = 4.0;

    /// <summary>Eine frische Default-Instanz (= bisherige hartkodierte Werte).</summary>
    public static ThreatScoreConfiguration Default() => new();

    // ---- Schwere-Keyword-Tabellen (FIX, nicht konfigurierbar – nur die Zahlen oben sind es) ----
    private static readonly string[] KindHeavy =
        { "mord", "tötung", "toetung", "hinrichtung", "geiselnahme", "entführung", "entfuehrung", "anschlag", "terror" };
    private static readonly string[] KindMedium =
        { "raub", "überfall", "ueberfall", "schießerei", "schiesserei", "bank", "erpressung", "schutzgeld", "waffenhandel", "drogenhandel" };

    /// <summary>Schweregewicht einer Aktivitäts-Art: schwer/mittel/sonst (NIE 0 – eine erfasste Tat ist immer Signal).</summary>
    public double KindWeight(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return KindWeightLight;
        }
        var a = kind.ToLowerInvariant();
        if (KindHeavy.Any(k => a.Contains(k)))
        {
            return KindWeightHeavy;
        }
        if (KindMedium.Any(k => a.Contains(k)))
        {
            return KindWeightMedium;
        }
        return KindWeightLight;
    }

    /// <summary>Gewicht eines Maßnahmen-Ausgangs.</summary>
    public double OutcomeWeight(MeasureOutcome outcome) => outcome switch
    {
        MeasureOutcome.Shot => OutcomeShot,
        MeasureOutcome.Injection => OutcomeInjection,
        MeasureOutcome.RunningStill => OutcomeRunningStill,
        _ => OutcomeReleased, // OffiziellEntlassen
    };
}
