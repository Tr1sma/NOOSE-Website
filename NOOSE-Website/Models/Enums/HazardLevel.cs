namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Gefährdungsstufe einer Fraktion, abgeleitet aus dem (Phase-8-)Bedrohungs-Score. Bewusst <b>on-read</b>
/// berechnet (kein gespeichertes Feld, kein Hintergrund-Job) – Vorbild <c>LebensstatusLogic</c>. Solange der
/// Score noch nicht berechnet wird (Phase 8), liefern alle Fraktionen <see cref="Keine"/>.
/// </summary>
public enum HazardLevel
{
    No = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>
/// Mapping Bedrohungs-Score → <see cref="GefaehrdungsStufe"/> und Anzeigetexte (UI-frei). Die Schwellen sind die
/// <b>einzige Quelle der Wahrheit</b>: die Phase-8-Score-Berechnung richtet sich nach diesem Raster aus.
/// </summary>
public static class HazardLevelLogic
{
    /// <summary>
    /// Bildet einen Bedrohungs-Score (0–100, <c>null</c> = noch nicht bewertet) auf eine Stufe ab.
    /// Schwellen: ≤0/null → Keine · 1–24 → Niedrig · 25–49 → Mittel · 50–74 → Hoch · ≥75 → Kritisch.
    /// </summary>
    public static HazardLevel From(int? score) => score switch
    {
        null or <= 0 => HazardLevel.No,
        < 25 => HazardLevel.Low,
        < 50 => HazardLevel.Medium,
        < 75 => HazardLevel.High,
        _ => HazardLevel.Critical,
    };

    public static string Name(HazardLevel level) => level switch
    {
        HazardLevel.No => "Keine",
        HazardLevel.Low => "Niedrig",
        HazardLevel.Medium => "Mittel",
        HazardLevel.High => "Hoch",
        HazardLevel.Critical => "Kritisch",
        _ => "—",
    };

    public static readonly IReadOnlyList<HazardLevel> All = new[]
    {
        HazardLevel.No,
        HazardLevel.Low,
        HazardLevel.Medium,
        HazardLevel.High,
        HazardLevel.Critical,
    };
}
