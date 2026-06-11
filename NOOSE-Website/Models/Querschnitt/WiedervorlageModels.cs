namespace NOOSE_Website.Models.Querschnitt;

/// <summary>Eingabe zum Anlegen/Bearbeiten einer Wiedervorlage (aus dem Dialog).</summary>
public sealed record WiedervorlageEingabe(DateTime FaelligAm, string? Notiz, string? ZustaendigerAgentId);

/// <summary>Eine Wiedervorlage, aufbereitet für das Panel an einer Akte (Zeit lokal anzeigen).</summary>
public sealed record WiedervorlageItem(
    string Id,
    DateTime FaelligAm,
    string? Notiz,
    string? ZustaendigerAgentId,
    string? ZustaendigerCodename,
    bool Erledigt,
    DateTime? ErledigtAm,
    bool Ueberfaellig,
    bool DarfBearbeiten);

/// <summary>Eine fällige Wiedervorlage des Aufrufers, aufgelöst für die Dashboard-Liste.</summary>
public sealed record WiedervorlageDashboardItem(
    string Id,
    string Anzeige,
    string? Href,
    DateTime FaelligAm,
    string? Notiz);
