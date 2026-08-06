using NOOSE_Website.Data.Entities.Abductions;
using NOOSE_Website.Data.Entities.Absences;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Activities;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Flattens each deletable record type onto the shared trash row.</summary>
public static class TrashProjection
{
    private const string Dash = "—";

    public static TrashItem Person(Person x) => new("personen", x.Id, x.CaseNumber, x.Name, null, x.DeletedAt);

    public static TrashItem Faction(Faction x) => new("fraktionen", x.Id, x.CaseNumber, x.Name, null, x.DeletedAt);

    public static TrashItem Group(PersonGroup x) => new("personengruppen", x.Id, x.CaseNumber, x.Name, null, x.DeletedAt);

    public static TrashItem Party(Party x) => new("parteien", x.Id, x.CaseNumber, x.Name, null, x.DeletedAt);

    public static TrashItem Taskforce(Taskforce x) => new("taskforces", x.Id, x.CaseNumber, x.Name, null, x.DeletedAt);

    public static TrashItem Case(Case x) => new("vorgaenge", x.Id, x.CaseNumber, x.Title, null, x.DeletedAt);

    public static TrashItem Operation(Operation x) => new("operationen", x.Id, x.CaseNumber, x.Title, null, x.DeletedAt);

    public static TrashItem Job(Job x) => new("aufgaben", x.Id, x.CaseNumber, x.Title, null, x.DeletedAt);

    public static TrashItem Announcement(Announcement x) => new("brett", x.Id, x.CaseNumber, x.Title, null, x.DeletedAt);

    public static TrashItem Appointment(Appointment x)
        => new("kalender", x.Id, x.CaseNumber, x.Title, Moment(x.Start), x.DeletedAt);

    public static TrashItem Meeting(Meeting x)
        => new("besprechungen", x.Id, x.CaseNumber, x.Title,
            Join(Moment(x.Start), x.Location, MeetingStatusDisplay.Name(x.Status)), x.DeletedAt);

    public static TrashItem Activity(AgentActivityListItem x)
        => new("aktivitaeten", x.Id, null, x.Title, x.OwnerName, x.DeletedAt);

    // absences carry no Aktenzeichen, so the agent identifies the row and the period is the detail
    public static TrashItem Absence(Absence x)
        => new("abmeldungen", x.Id, null, x.Agent?.Codename ?? x.AgentId,
            Join($"{x.FromDate:dd.MM.yyyy} – {x.ToDate:dd.MM.yyyy}",
                 $"{x.Days} Tage",
                 AbsenceCategoryDisplay.Name(x.Category)),
            x.DeletedAt);

    // abductions identify by Aktenzeichen; the victim codename is the detail
    public static TrashItem Abduction(AgentAbduction x)
        => new("entfuehrungen", x.Id, x.CaseNumber, x.VictimAgent?.Codename ?? x.VictimAgentId,
            AbductionOutcomeDisplay.Name(x.Outcome), x.DeletedAt);

    private static string Moment(DateTime value) => value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    private static string? Join(params string?[] parts)
    {
        var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p) && p != Dash).ToArray();
        return kept.Length == 0 ? null : string.Join(" · ", kept);
    }
}
