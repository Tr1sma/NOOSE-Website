using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Abductions;

/// <summary>A record compromised by an abduction, resolved to a display name and route.</summary>
public record CompromisedRecord(
    string CompromiseId,
    string AbductionId,
    string AbductionCaseNumber,
    string TargetType,
    string TargetId,
    string TargetDisplay,
    string? TargetRoute,
    CompromiseStatus Status,
    string? Note,
    DateTime CompromisedAt);
