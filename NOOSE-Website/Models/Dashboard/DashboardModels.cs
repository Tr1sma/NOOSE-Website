using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Dashboard;

/// <summary>Welche Akten-Art im Lagezentrum betroffen ist (bestimmt Symbol, Label und Detail-Link).</summary>
public enum DashboardAkteTyp
{
    Person,
    Fraktion,
    Personengruppe,
    Partei,
    Operation,
    Taskforce,
    Vorgang,
}

/// <summary>
/// Die Kennzahl-Kacheln des Lagezentrums. Alle Zahlen sind aus Sicht des aufrufenden Agents
/// berechnet (Verschlusssachen-Filter), damit sie zu den jeweiligen Listenansichten passen.
/// </summary>
public record DashboardKennzahlen(
    int Personen,
    int FraktionenUndGruppen,
    int Operationen,
    int OffeneVorgaenge,
    int OffeneAntraege,
    int Verschlusssachen);

/// <summary>Ein einzelnes Segment einer Dashboard-Verteilung (eine Kategorie + ihre Anzahl).</summary>
/// <param name="Bezeichnung">Anzeigetext der Kategorie (z. B. „Verdachtsfall").</param>
/// <param name="Anzahl">Anzahl der Akten in dieser Kategorie (bereits VS-gefiltert).</param>
public record VerteilungSegment(string Bezeichnung, int Anzahl);

/// <summary>
/// Die vier Verteilungs-Diagramme des Lagezentrums (§248). Alle Zahlen sind aus Sicht des aufrufenden
/// Agents berechnet (Verschlusssachen-Filter), passend zu den Kennzahl-Kacheln. Die Segment-Reihenfolge ist
/// deterministisch (Enum-Reihenfolge bzw. feste Antrags-Arten), damit die UI je Diagramm stabile Farben zuordnen kann.
/// </summary>
public record DashboardVerteilungen(
    IReadOnlyList<VerteilungSegment> FaelleNachEinstufung,
    IReadOnlyList<VerteilungSegment> MassnahmeAusgaenge,
    IReadOnlyList<VerteilungSegment> FraktionenNachGefaehrdung,
    IReadOnlyList<VerteilungSegment> OffeneAntraegeNachArt);

/// <summary>
/// Ein Eintrag des Aktivitäts-Feeds „Letzte Änderungen". Aus einem Audit-Eintrag aufgelöst und auf die
/// zugehörige Eltern-Akte (Person/Fraktion/Personengruppe) hochgerollt – inkl. Anzeigename und Link.
/// </summary>
public record DashboardAenderung(
    DateTime Zeitpunkt,
    string? AgentName,
    AuditAktion Aktion,
    DashboardAkteTyp AkteTyp,
    string AkteId,
    string AkteName,
    string Aktenzeichen,
    /// <summary>Bei Kind-Änderungen (Dok, Mitglied, Agent-Zuteilung) die Art des Kindes; sonst null.</summary>
    string? Detail,
    /// <summary>True, wenn die Akte aktuell im Papierkorb liegt (dann keine Detail-Verlinkung).</summary>
    bool AkteGeloescht);
