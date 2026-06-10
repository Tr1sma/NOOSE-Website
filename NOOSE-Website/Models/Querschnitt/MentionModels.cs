namespace NOOSE_Website.Models.Querschnitt;

/// <summary>
/// Ein im Nachrichtentext gefundenes @-Verlinkungs-Token der Form <c>@{Typ:Id}</c>. <see cref="Start"/> und
/// <see cref="Laenge"/> beziehen sich auf die Position im Rohtext (für die Segmentierung).
/// </summary>
public readonly record struct MentionToken(string Typ, string Id, int Start, int Laenge);

/// <summary>
/// Ein Segment eines aufgelösten Mention-Textes. Entweder reiner Text (<see cref="IstVerweis"/> = false,
/// <see cref="Text"/> = Klartext) oder ein aufgelöster Verweis (<see cref="IstVerweis"/> = true,
/// <see cref="Text"/> = Anzeigename, <see cref="Typ"/> = Objekttyp für das Symbol, <see cref="Href"/> = Ziel
/// oder null, <see cref="Verborgen"/> = true wenn Verschlusssache für den Betrachter nicht sichtbar ist –
/// dann steht in <see cref="Text"/> kein sensibler Name).
/// </summary>
public record MentionSegment(bool IstVerweis, string Text, string? Typ = null, string? Href = null, bool Verborgen = false);

/// <summary>Ein Vorschlag im @-Picker beim Verlinken (Objekt zum Einfügen eines Tokens).</summary>
public record MentionTreffer(string Typ, string Id, string Anzeige, string? Sub);
