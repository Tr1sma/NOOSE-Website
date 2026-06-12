using NOOSE_Website.Services;

namespace NOOSE_Website.Models.Zeitstrahl;

/// <summary>
/// Kategorie eines Zeitstrahl-Ereignisses. Steuert Filter-Chip, Icon und Farbe im
/// <c>ZeitstrahlPanel</c>. Vereint die strukturellen Audit-Ereignisse (Anlage/Änderung/Löschung/
/// Mitgliedschaft/Zuteilung) mit den semantischen Domänen-Ereignissen (Einstufung, Doks, …).
/// </summary>
public enum ZeitstrahlKategorie
{
    Anlage,
    Aenderung,
    Loeschung,
    Wiederherstellung,
    Einstufung,
    Dok,
    Observation,
    Foto,
    Beziehung,
    Mitgliedschaft,
    Zuteilung,
    Verknuepfung,
    Kommentar,
    Quelle,
    Wiedervorlage,
    Aktivitaet,
}

/// <summary>
/// Ein einzelnes Ereignis im vereinheitlichten Akten-Zeitstrahl. Rein lesend zusammengeführt aus
/// mehreren Quellen (Audit-Log + Einstufungs-/Mitglieds-/Verknüpfungs-/Kommentar-/Quellen-/…-Daten).
/// <see cref="Zeitpunkt"/> ist UTC (Sortier-Schlüssel); die Anzeige formatiert lokal. <see cref="Aenderungen"/>
/// trägt – nur bei Audit-Ereignissen – die Feld-für-Feld-Änderungen wie der bisherige „Historie"-Reiter.
/// </summary>
public sealed record ZeitstrahlEintrag(
    DateTime Zeitpunkt,
    ZeitstrahlKategorie Kategorie,
    string Titel,
    string? Detail,
    string? AkteurName,
    string? Href,
    IReadOnlyList<AuditAnzeige.Feldaenderung>? Aenderungen = null);
