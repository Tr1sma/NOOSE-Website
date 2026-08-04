using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Informants;

/// <summary>Display projection of an informant. RealName/ContactInfo/Notes are populated ONLY when the viewer may see
/// the identity — they are left null otherwise so the real name never crosses the SignalR wire.</summary>
public sealed record InformantDisplay(
    string Id, string CaseNumber, string Codename, string? Description,
    InformantReliability Reliability, InformantStatus Status,
    string HandlerId, string? HandlerName,
    bool MaySeeIdentity, string? RealName, string? ContactInfo, string? Notes,
    bool MayEdit);

/// <summary>A logged meeting for display.</summary>
public sealed record InformantMeetingDisplay(string Id, DateTime MeetingDate, string? Location, string? Content, string? AuthorName);

/// <summary>Create/update input for an informant.</summary>
public sealed record InformantInput(
    string Codename, string? Description, InformantReliability Reliability, InformantStatus Status,
    string HandlerId, string? RealName, string? ContactInfo, string? Notes);

/// <summary>Create input for a meeting.</summary>
public sealed record InformantMeetingInput(DateTime MeetingDate, string? Location, string? Content);

/// <summary>An agent selectable as an informant handler.</summary>
public sealed record InformantHandlerOption(string Id, string Name);
