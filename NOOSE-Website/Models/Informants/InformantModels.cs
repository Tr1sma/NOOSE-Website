using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Informants;

/// <summary>Display projection of an informant. Everything here is open to whoever may see the record at all.</summary>
public sealed record InformantDisplay(
    string Id, string CaseNumber, string Name, string? Description,
    InformantReliability Reliability, InformantStatus Status,
    string HandlerId, string? HandlerName,
    string? PersonId, string? PersonName, string? PersonCaseNumber,
    string? FactionId, string? FactionName,
    string? ContactInfo, string? Notes,
    bool MayEdit);

/// <summary>A logged meeting for display.</summary>
public sealed record InformantMeetingDisplay(string Id, DateTime MeetingDate, string? Location, string? Content, string? AuthorName);

/// <summary>Create/update input for an informant. Either <paramref name="PersonId"/> or <paramref name="RealName"/> must be set.</summary>
public sealed record InformantInput(
    string? RealName, string? PersonId, string? FactionId, string? Description,
    InformantReliability Reliability, InformantStatus Status,
    string HandlerId, string? ContactInfo, string? Notes);

/// <summary>Create input for a meeting.</summary>
public sealed record InformantMeetingInput(DateTime MeetingDate, string? Location, string? Content);

/// <summary>An agent selectable as an informant handler.</summary>
public sealed record InformantHandlerOption(string Id, string Name);

/// <summary>Informant marker shown on a linked person record. MayOpen tells whether the viewer may open the V-person file.</summary>
public sealed record InformantPersonMarker(string InformantId, string CaseNumber, InformantStatus Status, bool MayOpen);

/// <summary>An informant listed on a faction record. Name falls back to the case number for viewers without record access.</summary>
public sealed record InformantFactionEntry(
    string InformantId, string CaseNumber, string Name, InformantStatus Status, bool MayOpen);
