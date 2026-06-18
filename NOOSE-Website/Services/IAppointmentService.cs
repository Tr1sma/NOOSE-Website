using System.Security.Claims;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Models.Appointments;

namespace NOOSE_Website.Services;

/// <summary>Appointment/calendar records. Like a task: unrestricted ones are visible to all active agents; restricted ones only to creator, assigned participants, and supervision. Times are stored as UTC (input is local RP time).</summary>
public interface IAppointmentService
{
    /// <summary>Load an appointment; null if restricted and not visible to the caller.</summary>
    Task<Appointment?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<Appointment>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>Appointment search for pickers; restricted ones only for participants/supervision.</summary>
    Task<List<Appointment>> SearchAsync(string? searchText, bool mayAll, string? meId, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Create an appointment, assign it to the given active agents, and notify them (except the creator).</summary>
    Task<Appointment> CreateAsync(AppointmentInput input, IReadOnlyList<string> agentIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Edit master data; creator or leadership only.</summary>
    Task RefreshAsync(string id, AppointmentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Participants assigned to the appointment (with agent data; by codename).</summary>
    Task<List<AppointmentAssignment>> GetParticipantAsync(string appointmentId, CancellationToken cancellationToken = default);

    /// <summary>Assign a participant; creator or leadership only; notifies the agent (unless they are the actor).</summary>
    Task AgentAssignAsync(string appointmentId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Remove a participant; creator or leadership only.</summary>
    Task AgentRemoveAsync(string assignmentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
