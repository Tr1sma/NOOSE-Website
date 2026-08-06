using NOOSE_Website.Data.Entities.Abductions;

namespace NOOSE_Website.Models.Abductions;

/// <summary>Display model for an abduction with resolved victim codename and perpetrator name/route.</summary>
public record AbductionDisplay(
    AgentAbduction Abduction,
    string VictimCodename,
    string? PerpetratorName,
    string? PerpetratorRoute,
    int CompromisedActiveCount);
