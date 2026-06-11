namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Gefährdungsstufe einer Fraktion, abgeleitet aus dem (Phase-8-)Bedrohungs-Score. Bewusst <b>on-read</b>
/// berechnet (kein gespeichertes Feld, kein Hintergrund-Job) – Vorbild <c>LebensstatusLogic</c>. Solange der
/// Score noch nicht berechnet wird (Phase 8), liefern alle Fraktionen <see cref="Keine"/>.
/// </summary>
public enum GefaehrdungsStufe
{
    Keine = 0,
    Niedrig = 1,
    Mittel = 2,
    Hoch = 3,
    Kritisch = 4,
}

/// <summary>
/// Mapping Bedrohungs-Score → <see cref="GefaehrdungsStufe"/> und Anzeigetexte (UI-frei). Die Schwellen sind die
/// <b>einzige Quelle der Wahrheit</b>: die Phase-8-Score-Berechnung richtet sich nach diesem Raster aus.
/// </summary>
public static class GefaehrdungsStufeLogic
{
    /// <summary>
    /// Bildet einen Bedrohungs-Score (0–100, <c>null</c> = noch nicht bewertet) auf eine Stufe ab.
    /// Schwellen: ≤0/null → Keine · 1–24 → Niedrig · 25–49 → Mittel · 50–74 → Hoch · ≥75 → Kritisch.
    /// </summary>
    public static GefaehrdungsStufe Aus(int? score) => score switch
    {
        null or <= 0 => GefaehrdungsStufe.Keine,
        < 25 => GefaehrdungsStufe.Niedrig,
        < 50 => GefaehrdungsStufe.Mittel,
        < 75 => GefaehrdungsStufe.Hoch,
        _ => GefaehrdungsStufe.Kritisch,
    };

    public static string Name(GefaehrdungsStufe stufe) => stufe switch
    {
        GefaehrdungsStufe.Keine => "Keine",
        GefaehrdungsStufe.Niedrig => "Niedrig",
        GefaehrdungsStufe.Mittel => "Mittel",
        GefaehrdungsStufe.Hoch => "Hoch",
        GefaehrdungsStufe.Kritisch => "Kritisch",
        _ => "—",
    };

    public static readonly IReadOnlyList<GefaehrdungsStufe> Alle = new[]
    {
        GefaehrdungsStufe.Keine,
        GefaehrdungsStufe.Niedrig,
        GefaehrdungsStufe.Mittel,
        GefaehrdungsStufe.Hoch,
        GefaehrdungsStufe.Kritisch,
    };
}
