namespace NOOSE_Website.Models.Threat;

/// <summary>One score/confidence point on a record's timeline.</summary>
public readonly record struct ThreatScorePoint(DateTime Timestamp, int? Score, int? Confidence);

/// <summary>Ranked factions as of a given month (bar-chart-race frame).</summary>
public sealed record ThreatRaceFrame(DateTime Month, IReadOnlyList<ThreatRaceEntry> Entries);

/// <summary>One faction inside a race frame.</summary>
public sealed record ThreatRaceEntry(string EntityId, string Name, int Score, bool Classified);

/// <summary>A record whose score moved the most within the window.</summary>
public sealed record ThreatMover(
    string EntityType, string EntityId, string Name,
    int FromScore, int ToScore, int Delta, bool Classified, string Href);
