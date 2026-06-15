using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Statistics;

/// <summary>
/// Ein Eintrag einer Top-Liste der Statistik-Seite (gefährlichste Personen bzw. Fraktionen). Score und
/// Stufe stammen aus dem (Phase-8-)Bedrohungs-Score; nur bewertete Akten (Score &gt; 0) erscheinen.
/// </summary>
/// <param name="Name">Anzeigename der Akte.</param>
/// <param name="Aktenzeichen">Aktenzeichen der Akte.</param>
/// <param name="Href">Detail-Link der Akte (z. B. <c>/personen/{id}</c>).</param>
/// <param name="Score">Bedrohungs-Score 0–100.</param>
/// <param name="Stufe">Aus dem Score abgeleitete Gefährdungsstufe.</param>
public record StatisticsTopEntry(string Name, string CaseNumber, string Href, int Score, HazardLevel Level);

/// <summary>
/// Ein Monatswert der 12-Monats-Zeitreihe: erfasste Maßnahmen (Personen-Doks nach RP-Zeitpunkt) und
/// Neuzugänge (neu angelegte Personenakten nach Erfassungszeit) im jeweiligen Monat.
/// </summary>
/// <param name="Jahr">Kalenderjahr des Monats.</param>
/// <param name="Monat">Kalendermonat (1–12).</param>
/// <param name="Label">Kurzes Anzeige-Etikett, z. B. „Jun 26".</param>
/// <param name="Massnahmen">Anzahl der Personen-Doks im Monat.</param>
/// <param name="Neuzugaenge">Anzahl neu erfasster Personenakten im Monat.</param>
public record StatisticsMonth(int Year, int Month, string Label, int Measures, int NewEntries);

/// <summary>
/// Die aggregierten Auswertungen der Statistik-/Lagezentrum-Seite (§300 Block D). Alle Zahlen sind aus
/// Sicht des aufrufenden Agents berechnet (Verschlusssachen-Filter): Nicht-Führung zählt nur
/// nicht-klassifizierte Akten, damit aus den Diagrammen/Listen kein Verschlusssachen-Bestand ablesbar ist.
/// Die Segment-Reihenfolge ist deterministisch (Enum-Reihenfolge), damit die UI stabile Farben zuordnen kann.
/// </summary>
public record StatisticsReport(
    DashboardMetrics Metrics,
    IReadOnlyList<DistributionSegment> PeopleByClassification,
    IReadOnlyList<DistributionSegment> PeopleByHazard,
    IReadOnlyList<DistributionSegment> PeopleByLifeStatus,
    IReadOnlyList<DistributionSegment> FactionsByHazard,
    IReadOnlyList<DistributionSegment> MeasureOutcomes,
    IReadOnlyList<DistributionSegment> CasesByStatus,
    IReadOnlyList<StatisticsTopEntry> TopPeople,
    IReadOnlyList<StatisticsTopEntry> TopFactions,
    IReadOnlyList<StatisticsMonth> TimeSeries);

/// <summary>
/// Kopfzeile eines archivierten Lageberichts für die Archiv-Liste (ohne den schweren JSON-Snapshot).
/// </summary>
/// <param name="Id">Id des Lageberichts (Detail-Link).</param>
/// <param name="Jahr">Berichtsjahr.</param>
/// <param name="Monat">Berichtsmonat (1–12).</param>
/// <param name="Titel">Anzeigetitel, z. B. „Lagebericht Juni 2026".</param>
/// <param name="ErzeugtAm">Zeitpunkt der Erzeugung (= Berichtsstand, UTC).</param>
/// <param name="ErzeugtVon">Codename des auslösenden Agents oder <c>null</c> (automatisch erzeugt).</param>
public record SituationReportHead(string Id, int Year, int Month, string Title, DateTime GeneratedAt, string? GeneratedBy);

/// <summary>
/// Ein archivierter Lagebericht für die Detailansicht: Kopfdaten + der deserialisierte, eingefrorene
/// <see cref="StatistikReport"/>-Schnappschuss aus dem Erzeugungszeitpunkt.
/// </summary>
/// <param name="Id">Id des Lageberichts.</param>
/// <param name="Titel">Anzeigetitel.</param>
/// <param name="ErzeugtAm">Berichtsstand (Zeitpunkt der Erzeugung, UTC).</param>
/// <param name="ErzeugtVon">Codename des auslösenden Agents oder <c>null</c> (automatisch erzeugt).</param>
/// <param name="Report">Der eingefrorene Auswertungs-Schnappschuss.</param>
public record SituationReportDisplay(string Id, string Title, DateTime GeneratedAt, string? GeneratedBy, StatisticsReport Report);
