using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Dashboard;

/// <summary>Welche Akten-Art im Lagezentrum betroffen ist (bestimmt Symbol, Label und Detail-Link).</summary>
public enum DashboardAkteTyp
{
    Person,
    Fraktion,
    Personengruppe,
    Partei,
}

/// <summary>
/// Die vier Kennzahl-Kacheln des Lagezentrums. Alle Zahlen sind aus Sicht des aufrufenden Agents
/// berechnet (Verschlusssachen-Filter), damit sie zu den jeweiligen Listenansichten passen.
/// </summary>
public record DashboardKennzahlen(
    int Personen,
    int FraktionenUndGruppen,
    int OffeneAntraege,
    int Verschlusssachen);

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
