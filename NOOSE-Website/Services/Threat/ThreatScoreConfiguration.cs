using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Runtime score configuration.</summary>
public sealed class ThreatScoreConfiguration
{
    // ---- Shared ----
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

    // ---- Faction S1 ----
    public double CapS1 { get; set; } = 55.0;
    public double S1Denominator { get; set; } = 6.0;
    public double DocHeatWeight { get; set; } = 0.6;
    public double PerMemberDocCap { get; set; } = 8.0;

    // ---- Faction S2 ----
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

    // ---- Faction S3 ----
    public double CapS3 { get; set; } = 15.0;
    public double S3Denominator { get; set; } = 4.0;
    public double ConflictWeight { get; set; } = 2.0;
    public double AllianceWeight { get; set; } = 1.0;

    // ---- Faction S4 ----
    public double CapS4 { get; set; } = 8.0;
    public double S4Denominator { get; set; } = 4.0;

    // ---- Person P1 ----
    public double CapP1 { get; set; } = 40.0;
    public double P1Denominator { get; set; } = 4.0;

    // ---- Person P2 ----
    public double CapP2 { get; set; } = 22.0;
    public double PersonCapWeapons { get; set; } = 14.0;
    public double PersonWeaponsDenominator { get; set; } = 2.0;
    public double FugitivePoints { get; set; } = 8.0;

    // ---- Person P3 ----
    public double CapP3 { get; set; } = 18.0;
    public double P3Denominator { get; set; } = 3.0;
    /// <summary>Completed observation weight.</summary>
    public double ObservationCompletedWeight { get; set; } = 0.6;

    // ---- Person P4 ----
    public double CapP4 { get; set; } = 12.0;
    public double P4Denominator { get; set; } = 4.0;
    public double EnemyWeight { get; set; } = 2.0;
    public double AllyWeight { get; set; } = 1.0;
    public double GpWeight { get; set; } = 1.0;
    public double LeadWeight { get; set; } = 1.5;

    // ---- Person P5 ----
    public double CapP5 { get; set; } = 8.0;
    public double P5Denominator { get; set; } = 4.0;

    /// <summary>Default instance.</summary>
    public static ThreatScoreConfiguration Default() => new();

    // ---- Severity keywords (fixed) ----
    private static readonly string[] KindHeavy =
        { "mord", "tötung", "toetung", "hinrichtung", "geiselnahme", "entführung", "entfuehrung", "anschlag", "terror" };
    private static readonly string[] KindMedium =
        { "raub", "überfall", "ueberfall", "schießerei", "schiesserei", "bank", "erpressung", "schutzgeld", "waffenhandel", "drogenhandel" };

    /// <summary>Activity kind weight.</summary>
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

    /// <summary>Outcome weight.</summary>
    public double OutcomeWeight(MeasureOutcome outcome) => outcome switch
    {
        MeasureOutcome.Shot => OutcomeShot,
        MeasureOutcome.Injection => OutcomeInjection,
        MeasureOutcome.RunningStill => OutcomeRunningStill,
        _ => OutcomeReleased, // released
    };
}
