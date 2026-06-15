namespace NOOSE_Website.Models.Common;

/// <summary>
/// Ein im Nachrichtentext gefundenes @-Verlinkungs-Token der Form <c>@{Typ:Id}</c>. <see cref="Start"/> und
/// <see cref="Laenge"/> beziehen sich auf die Position im Rohtext (für die Segmentierung).
/// </summary>
public readonly record struct MentionToken(string Type, string Id, int Start, int Length);

/// <summary>
/// Ein Segment eines aufgelösten Mention-Textes. Entweder reiner Text (<see cref="IstVerweis"/> = false,
/// <see cref="Text"/> = Klartext) oder ein aufgelöster Verweis (<see cref="IstVerweis"/> = true,
/// <see cref="Text"/> = Anzeigename, <see cref="Typ"/> = Objekttyp für das Symbol, <see cref="Href"/> = Ziel
/// oder null, <see cref="Verborgen"/> = true wenn Verschlusssache für den Betrachter nicht sichtbar ist –
/// dann steht in <see cref="Text"/> kein sensibler Name).
/// </summary>
public record MentionSegment(bool IsReference, string Text, string? Type = null, string? Href = null, bool Hidden = false);

/// <summary>Ein Vorschlag im @-Picker beim Verlinken (Objekt zum Einfügen eines Tokens).</summary>
public record MentionHit(string Type, string Id, string Display, string? Sub);
