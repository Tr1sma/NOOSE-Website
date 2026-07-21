using System.Security.Claims;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Meetings;

namespace NOOSE_Website.Services;

/// <summary>Meetings, agenda, minutes and the attendance roster; the agenda sits behind the Senior Special Agent gate.</summary>
public interface IMeetingService
{
    Task<List<MeetingListItem>> GetListAsync(string? meId, DateOnly? from = null, DateOnly? to = null,
        int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    Task<Meeting?> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<List<Meeting>> SearchAsync(string? searchText, int max = 20, CancellationToken cancellationToken = default);

    Task<List<Meeting>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task<Meeting> CreateAsync(MeetingInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, MeetingInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Clones the meeting 14 days later with its unchecked agenda items and their links.</summary>
    Task<Meeting> NextCreateAsync(string sourceMeetingId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>True if a follow-up meeting was already cloned from this one.</summary>
    Task<bool> HasNextAsync(string meetingId, CancellationToken cancellationToken = default);

    Task<List<MeetingAgendaItem>> GetAgendaAsync(string meetingId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task<string?> GetItemNoteAsync(string itemId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>All agenda-item links of one meeting in a single round trip, filtered like LinkService would.</summary>
    Task<Dictionary<string, IReadOnlyList<LinkDisplay>>> GetAgendaLinksAsync(string meetingId, ViewerScope scope,
        CancellationToken cancellationToken = default);

    Task<MeetingAgendaItem> AgendaItemCreateAsync(string meetingId, MeetingAgendaItemInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task AgendaItemRefreshAsync(string itemId, MeetingAgendaItemInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task AgendaItemRemoveAsync(string itemId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Moves an item one slot up (delta -1) or down (delta +1).</summary>
    Task AgendaItemMoveAsync(string itemId, int delta, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task AgendaItemNoteAsync(string itemId, string? html, bool done, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task MinutesAsync(string meetingId, string? html, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<MeetingAttendanceRow>> GetAttendanceAsync(string meetingId, bool mayReason, string? meId,
        CancellationToken cancellationToken = default);

    Task AttendanceSetAsync(string meetingId, string agentId, MeetingAttendanceStatus status, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Freezes the roster; refuses an all-missing close unless explicitly confirmed.</summary>
    Task CloseAttendanceAsync(string meetingId, bool confirmAllMissing, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Drops the snapshot so the meeting stops occupying an evaluation slot.</summary>
    Task ReopenAttendanceAsync(string meetingId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SignOffAsync(string meetingId, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SignOffRevokeAsync(string meetingId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
