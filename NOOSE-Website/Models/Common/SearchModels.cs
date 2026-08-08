using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Activities;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Abductions;
using NOOSE_Website.Data.Entities.Evidence;
using NOOSE_Website.Data.Entities.Financing;
using NOOSE_Website.Data.Entities.Kasse;
using NOOSE_Website.Data.Entities.Recruiting;

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
public record SearchHit(string Category, string TargetId, string Title, string Snippet, string CaseNumber, string? TargetType = null)
{
    /// <summary>Stripped here rather than at the ~20 call sites: result lists render the snippet raw and never resolve mention tokens.</summary>
    public string Snippet { get; init; } = NOOSE_Website.Services.MentionParser.Strip(Snippet);
}

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
        nameof(AgentActivity) => $"/aktivitaeten/{targetId}",
        nameof(Taskforce) => $"/taskforces/{targetId}",
        nameof(Case) => $"/vorgaenge/{targetId}",
        nameof(Job) => $"/aufgaben/{targetId}",
        nameof(Appointment) => $"/kalender/{targetId}",
        nameof(Meeting) => $"/besprechungen/{targetId}",
        nameof(Document) => $"/dokumente/{targetId}",
        nameof(Law) => $"/gesetze/{targetId}",
        nameof(Agent) => $"/personal/{targetId}",
        nameof(AgentAbduction) => $"/entfuehrungen/{targetId}",
        nameof(EvidenceItem) => $"/asservatenkammer/item/{targetId}",
        nameof(EvidenceEntry) => $"/asservatenkammer/eintrag/{targetId}",
        nameof(KassenBuchung) => $"/kasse/buchung/{targetId}",
        nameof(Bewerbung) => $"/bewerbungen/{targetId}",
        nameof(FinancingRequest) => $"/finanzierungen/{targetId}",
        _ => $"/personen/{targetId}",
    };

    /// <summary>Types <see cref="Route"/> maps explicitly. Everything else falls through to the person route,
    /// which is right for a search hit (its category is always one of these or a person) but wrong for a caller
    /// that must not produce a link into the wrong record — that caller asks here first.</summary>
    private static readonly HashSet<string> Routed = new(StringComparer.Ordinal)
    {
        "Person", nameof(Faction), nameof(PersonGroup), nameof(Party), nameof(Operation),
        nameof(AgentActivity), nameof(Taskforce), nameof(Case), nameof(Job), nameof(Appointment),
        nameof(Meeting), nameof(Document), nameof(Law), nameof(Agent), nameof(AgentAbduction),
        nameof(EvidenceItem), nameof(EvidenceEntry), nameof(KassenBuchung), nameof(Bewerbung),
        nameof(FinancingRequest),
    };

    /// <summary>Whether <see cref="Route"/> has a real route for this type rather than the person fallback.</summary>
    public static bool Knows(string? recordsType) => recordsType is not null && Routed.Contains(recordsType);

    /// <summary>Route of a hit: explicit target type, else category.</summary>
    public static string Route(SearchHit hit) => Route(hit.TargetType ?? hit.Category, hit.TargetId);
}
