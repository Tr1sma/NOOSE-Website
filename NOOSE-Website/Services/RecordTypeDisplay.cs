using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Activities;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>German label for a record's CLR type name. Used wherever a polymorphic reference is shown.</summary>
public static class RecordTypeDisplay
{
    /// <summary>Label for the type, or the raw type name when unknown.</summary>
    public static string Name(string recordType) => recordType switch
    {
        nameof(Person) => "Person",
        nameof(Faction) => "Fraktion",
        nameof(PersonGroup) => "Personengruppe",
        nameof(Party) => "Partei",
        nameof(Operation) => "Operation",
        nameof(Case) => "Vorgang",
        nameof(Taskforce) => "Taskforce",
        nameof(Job) => "Aufgabe",
        nameof(Document) => "Dokument",
        nameof(Law) => "Gesetz",
        nameof(Appointment) => "Termin",
        nameof(Meeting) => "Besprechung",
        nameof(AgentActivity) => "Aktivität",
        nameof(Agent) => "Agent",
        _ => recordType,
    };
}
