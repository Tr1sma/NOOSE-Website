using NOOSE_Website.Data.Entities.Abductions;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>Maps audit events to timeline categories/titles. Shared by per-record timeline + global chronicle.</summary>
public static class TimelineDisplay
{
    /// <summary>Category + title for an audit row of the given entity type/action.</summary>
    public static (TimelineCategory Kat, string Title) MapAudit(string entityType, AuditAction action)
    {
        string Verb(string created, string deleted) => action switch
        {
            AuditAction.Created => created,
            AuditAction.Modified => "geändert",
            AuditAction.Deleted => deleted,
            AuditAction.Restored => "wiederhergestellt",
            _ => action.ToString(),
        };

        if (entityType == nameof(PersonDoc))
        {
            return (TimelineCategory.Doc, $"Dok {Verb("angelegt", "gelöscht")}");
        }
        if (entityType is nameof(FactionMember) or nameof(PersonGroupMember) or nameof(PartyMember))
        {
            return (TimelineCategory.Membership, $"Mitglied {Verb("aufgenommen", "entfernt")}");
        }
        if (entityType is nameof(FactionAgent) or nameof(PersonGroupAgent) or nameof(PartyAgent)
            or nameof(OperationAgent) or nameof(CaseAgent) or nameof(TaskforceAgent) or nameof(JobAssignment)
            or nameof(AppointmentAssignment))
        {
            return (TimelineCategory.Allocation, $"Agent {Verb("zugeteilt", "entfernt")}");
        }
        if (entityType is nameof(PersonPhoto) or nameof(FactionPhoto))
        {
            return (TimelineCategory.Photo, $"Foto {Verb("hinzugefügt", "entfernt")}");
        }
        if (entityType == nameof(CustomFieldValue))
        {
            return (TimelineCategory.Change, $"Sonderfeld {Verb("gesetzt", "entfernt")}");
        }
        if (entityType == nameof(MeetingAgendaItem))
        {
            return (TimelineCategory.Agenda, $"Tagesordnungspunkt {Verb("angelegt", "gelöscht")}");
        }
        if (entityType == nameof(MeetingAttendance))
        {
            return (TimelineCategory.Attendance, $"Anwesenheit {Verb("erfasst", "entfernt")}");
        }
        if (entityType == nameof(MeetingSignOff))
        {
            return (TimelineCategory.SignOff, $"Abmeldung {Verb("eingetragen", "entfernt")}");
        }
        if (entityType == nameof(Comment))
        {
            return (TimelineCategory.Comment, $"Kommentar {Verb("geschrieben", "gelöscht")}");
        }
        if (entityType == nameof(Source))
        {
            return (TimelineCategory.Source, $"Quelle {Verb("hinzugefügt", "entfernt")}");
        }
        if (entityType == nameof(Followup))
        {
            return (TimelineCategory.Followup, $"Wiedervorlage {Verb("angelegt", "gelöscht")}");
        }
        if (entityType == nameof(PersonRelation))
        {
            return (TimelineCategory.Relation, $"Beziehung {Verb("angelegt", "entfernt")}");
        }
        if (entityType == nameof(Link))
        {
            return (TimelineCategory.Link, $"Verknüpfung {Verb("angelegt", "entfernt")}");
        }
        if (entityType == nameof(Observation))
        {
            return (TimelineCategory.Observation, $"Observation {Verb("erfasst", "gelöscht")}");
        }
        if (entityType == nameof(AbductionCompromise))
        {
            return (TimelineCategory.Link, $"Kompromittierung {Verb("markiert", "aufgehoben")}");
        }
        if (entityType == nameof(AgentAbduction))
        {
            return (TimelineCategory.Change, $"Entführung {Verb("dokumentiert", "gelöscht")}");
        }

        var kat = action switch
        {
            AuditAction.Created => TimelineCategory.Asset,
            AuditAction.Deleted => TimelineCategory.Deletion,
            AuditAction.Restored => TimelineCategory.Restoration,
            _ => TimelineCategory.Change,
        };
        return (kat, $"Akte {Verb("angelegt", "gelöscht")}");
    }

    /// <summary>Trim free text to a short timeline detail.</summary>
    public static string? Truncate(string? text, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        text = text.Trim();
        return text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
    }
}
