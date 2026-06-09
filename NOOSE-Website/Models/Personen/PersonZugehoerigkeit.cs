namespace NOOSE_Website.Models.Personen;

/// <summary>
/// Eine Zugehörigkeit einer Person zu einer übergeordneten Akte (Rück-Verknüpfung). <paramref name="Typ"/>
/// ist der CLR-Typname (<c>nameof(Fraktion)</c>/<c>nameof(Personengruppe)</c>); <paramref name="Rolle"/>
/// trägt den Fraktions-Rang bzw. die Gruppen-Rolle.
/// </summary>
public record PersonZugehoerigkeit(string Typ, string Id, string Name, string Aktenzeichen, string? Rolle, bool IstLeitung);
