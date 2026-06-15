using NOOSE_Website.Services;

namespace NOOSE_Website.Models.Timeline;

/// <summary>
/// Kategorie eines Zeitstrahl-Ereignisses. Steuert Filter-Chip, Icon und Farbe im
/// <c>ZeitstrahlPanel</c>. Vereint die strukturellen Audit-Ereignisse (Anlage/Änderung/Löschung/
/// Mitgliedschaft/Zuteilung) mit den semantischen Domänen-Ereignissen (Einstufung, Doks, …).
/// </summary>
public enum TimelineCategory
{
    Asset,
    Change,
    Deletion,
    Restoration,
    Classification,
    Doc,
    Observation,
    Photo,
    Relation,
    Membership,
    Allocation,
    Link,
    Comment,
    Source,
    Followup,
    Activity,
}

/// <summary>
/// Ein einzelnes Ereignis im vereinheitlichten Akten-Zeitstrahl. Rein lesend zusammengeführt aus
/// mehreren Quellen (Audit-Log + Einstufungs-/Mitglieds-/Verknüpfungs-/Kommentar-/Quellen-/…-Daten).
/// <see cref="Zeitpunkt"/> ist UTC (Sortier-Schlüssel); die Anzeige formatiert lokal. <see cref="Aenderungen"/>
/// trägt – nur bei Audit-Ereignissen – die Feld-für-Feld-Änderungen wie der bisherige „Historie"-Reiter.
/// </summary>
public sealed record TimelineEntry(
    DateTime Timestamp,
    TimelineCategory Category,
    string Title,
    string? Detail,
    string? ActorName,
    string? Href,
    IReadOnlyList<AuditDisplay.FieldChange>? Changes = null);
