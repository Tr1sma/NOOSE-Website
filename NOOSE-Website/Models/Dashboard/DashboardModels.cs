using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Dashboard;

/// <summary>Welche Akten-Art im Lagezentrum betroffen ist (bestimmt Symbol, Label und Detail-Link).</summary>
public enum DashboardRecordType
{
    Person,
    Faction,
    PersonGroup,
    Party,
    Operation,
    Taskforce,
    Case,
}

/// <summary>
/// Die Kennzahl-Kacheln des Lagezentrums. Alle Zahlen sind aus Sicht des aufrufenden Agents
/// berechnet (Verschlusssachen-Filter), damit sie zu den jeweiligen Listenansichten passen.
/// </summary>
public record DashboardMetrics(
    int People,
    int FactionsAndGroups,
    int Operations,
    int OpenCases,
    int OpenRequests,
    int Classified,
    int StaleRecords);

/// <summary>
/// Eine Akte mit Aktualisierungsbedarf (Ampel gelb/rot) für die Dashboard-Liste „was muss aktualisiert werden".
/// Aus Sicht des Aufrufers Verschlusssache-/Papierkorb-gefiltert.
/// </summary>
public record DashboardStaleRecord(
    DashboardRecordType Type,
    string Name,
    string CaseNumber,
    string Href,
    RecencyLevel Level,
    DateTime ReferenceUtc);

/// <summary>
/// Eine Fraktion mit ihrer Gefährdungsstufe für die Dashboard-Liste „Fraktionen nach Gefährdung" (echte Liste,
/// nicht aggregiert). Aus Sicht des Aufrufers VS-/Papierkorb-gefiltert; nach Gefährdung absteigend sortiert.
/// </summary>
public record DashboardFactionHazard(
    string Name,
    string CaseNumber,
    string Href,
    HazardLevel Level);

/// <summary>Ein einzelnes Segment einer Dashboard-Verteilung (eine Kategorie + ihre Anzahl).</summary>
/// <param name="Bezeichnung">Anzeigetext der Kategorie (z. B. „Verdachtsfall").</param>
/// <param name="Anzahl">Anzahl der Akten in dieser Kategorie (bereits VS-gefiltert).</param>
public record DistributionSegment(string Designation, int Count);

/// <summary>
/// Die vier Verteilungs-Diagramme des Lagezentrums (§248). Alle Zahlen sind aus Sicht des aufrufenden
/// Agents berechnet (Verschlusssachen-Filter), passend zu den Kennzahl-Kacheln. Die Segment-Reihenfolge ist
/// deterministisch (Enum-Reihenfolge bzw. feste Antrags-Arten), damit die UI je Diagramm stabile Farben zuordnen kann.
/// </summary>
public record DashboardDistributions(
    IReadOnlyList<DistributionSegment> CasesByClassification,
    IReadOnlyList<DistributionSegment> MeasureOutcomes,
    IReadOnlyList<DistributionSegment> FactionsByHazard,
    IReadOnlyList<DistributionSegment> OpenRequestsByKind);

/// <summary>
/// Ein Eintrag des Aktivitäts-Feeds „Letzte Änderungen". Aus einem Audit-Eintrag aufgelöst und auf die
/// zugehörige Eltern-Akte (Person/Fraktion/Personengruppe) hochgerollt – inkl. Anzeigename und Link.
/// </summary>
public record DashboardChange(
    DateTime Timestamp,
    string? AgentName,
    AuditAction Action,
    DashboardRecordType RecordType,
    string RecordId,
    string RecordName,
    string CaseNumber,
    /// <summary>Bei Kind-Änderungen (Dok, Mitglied, Agent-Zuteilung) die Art des Kindes; sonst null.</summary>
    string? Detail,
    /// <summary>True, wenn die Akte aktuell im Papierkorb liegt (dann keine Detail-Verlinkung).</summary>
    bool RecordDeleted);
