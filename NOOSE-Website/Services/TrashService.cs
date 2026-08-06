using System.Security.Claims;
using MudBlazor;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITrashService" />
public sealed class TrashService(
    IPersonService people,
    IFactionService factions,
    IPersonGroupService groups,
    IPartyService parties,
    ICaseService cases,
    IOperationService operations,
    ITaskforceService taskforces,
    IJobService jobs,
    IAnnouncementService announcements,
    IAppointmentService appointments,
    IMeetingService meetings,
    IAgentActivityService activities,
    IAbsenceService absences,
    IAbductionService abductions,
    IEvidenceService evidence) : ITrashService
{
    /// <summary>Loading and restoring for one record type; Restore stays a domain-service call.</summary>
    private sealed record TrashSource(
        TrashKind Kind,
        Func<CancellationToken, Task<List<TrashItem>>> Load,
        Func<string, ClaimsPrincipal, CancellationToken, Task> Restore);

    private static TrashSource Source<T>(
        TrashKind kind,
        Func<CancellationToken, Task<List<T>>> load,
        Func<T, TrashItem> project,
        Func<string, ClaimsPrincipal, CancellationToken, Task> restore)
        => new(kind, async ct => (await load(ct)).Select(project).ToList(), restore);

    private readonly IReadOnlyList<TrashSource> _sources =
    [
        Source(new TrashKind("personen", "Personen-Akten", Icons.Material.Filled.Badge, "/personen"),
            people.GetTrashAsync, TrashProjection.Person, people.RestoreAsync),
        Source(new TrashKind("fraktionen", "Fraktionen", Icons.Material.Filled.Groups, "/fraktionen"),
            factions.GetTrashAsync, TrashProjection.Faction, factions.RestoreAsync),
        Source(new TrashKind("personengruppen", "Personengruppen", Icons.Material.Filled.Diversity3, "/personengruppen"),
            groups.GetTrashAsync, TrashProjection.Group, groups.RestoreAsync),
        Source(new TrashKind("parteien", "Parteien", Icons.Material.Filled.AccountBalance, "/parteien"),
            parties.GetTrashAsync, TrashProjection.Party, parties.RestoreAsync),
        Source(new TrashKind("vorgaenge", "Vorgänge", Icons.Material.Filled.FolderSpecial, "/vorgaenge"),
            cases.GetTrashAsync, TrashProjection.Case, cases.RestoreAsync),
        Source(new TrashKind("operationen", "Operationen", Icons.Material.Filled.Radar, "/operationen"),
            operations.GetTrashAsync, TrashProjection.Operation, operations.RestoreAsync),
        Source(new TrashKind("taskforces", "Taskforces", Icons.Material.Filled.Groups2, "/taskforces"),
            taskforces.GetTrashAsync, TrashProjection.Taskforce, taskforces.RestoreAsync),
        Source(new TrashKind("aufgaben", "Aufgaben", Icons.Material.Filled.AssignmentTurnedIn, "/aufgaben"),
            jobs.GetTrashAsync, TrashProjection.Job, jobs.RestoreAsync),
        Source(new TrashKind("brett", "Ankündigungen", Icons.Material.Filled.Campaign, "/brett"),
            announcements.GetTrashAsync, TrashProjection.Announcement, announcements.RestoreAsync),
        Source(new TrashKind("kalender", "Termine", Icons.Material.Filled.CalendarMonth, "/kalender"),
            appointments.GetTrashAsync, TrashProjection.Appointment, appointments.RestoreAsync),
        Source(new TrashKind("besprechungen", "Besprechungen", Icons.Material.Filled.Groups, "/besprechungen"),
            meetings.GetTrashAsync, TrashProjection.Meeting, meetings.RestoreAsync),
        Source(new TrashKind("aktivitaeten", "Dienst-Aktivitäten", Icons.Material.Filled.Bolt, "/aktivitaeten"),
            activities.GetTrashAsync, TrashProjection.Activity, activities.RestoreAsync),
        Source(new TrashKind("abmeldungen", "Abmeldungen", Icons.Material.Filled.EventBusy, "/abmeldungen"),
            absences.GetTrashAsync, TrashProjection.Absence, absences.RestoreAsync),
        Source(new TrashKind("entfuehrungen", "Entführungen", Icons.Material.Filled.PersonOff, "/entfuehrungen"),
            abductions.GetTrashAsync, TrashProjection.Abduction, abductions.RestoreAsync),
        Source(new TrashKind("asservate-items", "Asservate (Items)", Icons.Material.Filled.Inventory2, "/asservatenkammer"),
            evidence.GetItemTrashAsync, TrashProjection.EvidenceItem, evidence.RestoreItemAsync),
        Source(new TrashKind("asservate-eintraege", "Asservate (Einträge)", Icons.Material.Filled.ReceiptLong, "/asservatenkammer"),
            evidence.GetEntryTrashAsync, TrashProjection.EvidenceEntry, evidence.RestoreEntryAsync),
    ];

    public IReadOnlyList<TrashKind> Kinds => _sources.Select(s => s.Kind).ToList();

    public async Task<List<TrashItem>> GetAsync(string kind, CancellationToken cancellationToken = default)
    {
        var rows = await Resolve(kind).Load(cancellationToken);
        return rows.OrderByDescending(r => r.DeletedAt).ToList();
    }

    public Task RestoreAsync(string kind, string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
        => Resolve(kind).Restore(id, actor, cancellationToken);

    private TrashSource Resolve(string kind)
        => _sources.FirstOrDefault(s => string.Equals(s.Kind.Key, kind, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unbekannter Papierkorb-Typ.");
}
