namespace NOOSE_Website.Models.Common;

/// <summary>Eingabe zum Anlegen/Bearbeiten einer Wiedervorlage (aus dem Dialog).</summary>
public sealed record FollowupInput(DateTime DueAt, string? Note, string? ResponsibleAgentId);

/// <summary>Eine Wiedervorlage, aufbereitet für das Panel an einer Akte (Zeit lokal anzeigen).</summary>
public sealed record FollowupItem(
    string Id,
    DateTime DueAt,
    string? Note,
    string? ResponsibleAgentId,
    string? ResponsibleCodename,
    bool Done,
    DateTime? DoneAt,
    bool Overdue,
    bool MayEdit);

/// <summary>Eine fällige Wiedervorlage des Aufrufers, aufgelöst für die Dashboard-Liste.</summary>
public sealed record FollowupDashboardItem(
    string Id,
    string Display,
    string? Href,
    DateTime DueAt,
    string? Note);
