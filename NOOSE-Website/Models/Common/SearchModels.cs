using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;

namespace NOOSE_Website.Models.Common;

/// <summary>
/// Kriterien einer globalen Suche. <see cref="Kategorien"/> enthält die CLR-Typnamen der zu
/// durchsuchenden Akten (leer = alle). <see cref="TagIds"/> schränkt auf Akten mit mindestens einem
/// dieser Tags ein. Wird für gespeicherte Suchen als JSON abgelegt (neue Flags sind abwärtskompatibel:
/// fehlen sie im JSON, gelten sie als false).
/// </summary>
public class SearchCriteria
{
    public string? Text { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> TagIds { get; set; } = new();

    /// <summary>Tippfehler-Toleranz: ergänzt die exakte Suche um ähnliche Treffer (Levenshtein, in-memory).</summary>
    public bool Fuzzy { get; set; }

    /// <summary>
    /// Max-Modus: durchsucht zusätzlich alle Nebenfelder (inkl. Person-Steckbrief) exakt, erzwingt
    /// Doks/Quellen/Kommentare und weitet die Tippfehler-Toleranz wortweise auf die Inhaltsfelder aus.
    /// </summary>
    public bool MaxMode { get; set; }
}

/// <summary>
/// Ein einzelner Suchtreffer. <see cref="Kategorie"/> ist der CLR-Typname der Trefferquelle (für die
/// Gruppierung). <see cref="ZielId"/> verweist auf die anzuspringende Akte; <see cref="ZielTyp"/> ist deren
/// Aktentyp (Person/Fraktion/Personengruppe) – bei Doks/Quellen/Kommentaren der Eltern-Akten-Typ. Ist
/// <see cref="ZielTyp"/> null, dient die Kategorie als Zieltyp (siehe <see cref="SuchNavigation"/>).
/// </summary>
public record SearchHit(string Category, string TargetId, string Title, string Snippet, string CaseNumber, string? TargetType = null);

/// <summary>Treffer einer Kategorie gebündelt (für die kategorisierte Ergebnisanzeige).</summary>
public record SearchResultGroup(string Category, string Display, List<SearchHit> Hit);

/// <summary>Kompakter Treffer für die Command-Palette (Schnellzugriff).</summary>
public record QuickHit(string Category, string TargetId, string Name, string CaseNumber);

/// <summary>Zielroute eines Treffers. Doks/Quellen/Kommentare verweisen auf ihre Eltern-Akte (Person/Fraktion/Gruppe).</summary>
public static class SearchNavigation
{
    public static string Route(string recordsType, string targetId) => recordsType switch
    {
        nameof(Faction) => $"/fraktionen/{targetId}",
        nameof(PersonGroup) => $"/personengruppen/{targetId}",
        nameof(Party) => $"/parteien/{targetId}",
        nameof(Operation) => $"/operationen/{targetId}",
        nameof(Taskforce) => $"/taskforces/{targetId}",
        nameof(Case) => $"/vorgaenge/{targetId}",
        nameof(Job) => $"/aufgaben/{targetId}",
        nameof(Appointment) => $"/kalender/{targetId}",
        nameof(Document) => $"/dokumente/{targetId}",
        nameof(Law) => $"/gesetze/{targetId}",
        _ => $"/personen/{targetId}",
    };

    /// <summary>Route eines Treffers: nutzt den expliziten Zieltyp, sonst die Kategorie.</summary>
    public static string Route(SearchHit hit) => Route(hit.TargetType ?? hit.Category, hit.TargetId);
}
