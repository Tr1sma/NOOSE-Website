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

/// <summary>Global search criteria; persisted as JSON for saved searches (missing flags default to false).</summary>
public class SearchCriteria
{
    public string? Text { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> TagIds { get; set; } = new();

    /// <summary>Typo tolerance via in-memory Levenshtein on top of exact search.</summary>
    public bool Fuzzy { get; set; }

    /// <summary>Also searches all side fields, forces docs/sources/comments, and extends fuzzy to content fields.</summary>
    public bool MaxMode { get; set; }
}

/// <summary>A single search hit. Category is the CLR type of the source; TargetType null means category is the target type.</summary>
public record SearchHit(string Category, string TargetId, string Title, string Snippet, string CaseNumber, string? TargetType = null);

/// <summary>Hits of one category bundled for grouped display.</summary>
public record SearchResultGroup(string Category, string Display, List<SearchHit> Hit);

/// <summary>Compact hit for the command palette.</summary>
public record QuickHit(string Category, string TargetId, string Name, string CaseNumber);

/// <summary>Target route of a hit; docs/sources/comments resolve to their parent record.</summary>
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

    /// <summary>Route of a hit: explicit target type, else category.</summary>
    public static string Route(SearchHit hit) => Route(hit.TargetType ?? hit.Category, hit.TargetId);
}
