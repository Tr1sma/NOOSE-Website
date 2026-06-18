namespace NOOSE_Website.Models.Common;

/// <summary>An @-mention token of the form <c>@{Type:Id}</c>; Start/Length index into the raw text.</summary>
public readonly record struct MentionToken(string Type, string Id, int Start, int Length);

/// <summary>A segment of resolved mention text: either plain text or a resolved reference. When Hidden is true the viewer may not see the classified target, so Text holds no sensitive name.</summary>
public record MentionSegment(bool IsReference, string Text, string? Type = null, string? Href = null, bool Hidden = false);

/// <summary>A suggestion in the @-mention picker.</summary>
public record MentionHit(string Type, string Id, string Display, string? Sub);
