namespace NOOSE_Website.Models.Personen;

/// <summary>
/// Eine Zugehörigkeit einer Person zu einer übergeordneten Akte (Rück-Verknüpfung). <paramref name="Typ"/>
/// ist der CLR-Typname (<c>nameof(Fraktion)</c>/<c>nameof(Personengruppe)</c>); <paramref name="MitgliedId"/>
/// ist die Id der Mitgliedschafts-Zeile (zum Entfernen); <paramref name="Id"/> die Id der Ziel-Akte;
/// <paramref name="Rolle"/> trägt den Fraktions-Rang bzw. die Gruppen-Rolle.
/// <paramref name="BeitrittAm"/> ist das Beitrittsdatum; <paramref name="BeendetAm"/> das Austrittsdatum
/// (<c>null</c> = noch aktiv) – für die Verlaufsanzeige ehemaliger Zugehörigkeiten.
/// </summary>
public record PersonZugehoerigkeit(string Typ, string MitgliedId, string Id, string Name, string Aktenzeichen,
    string? Rolle, bool IstLeitung, DateTime BeitrittAm, DateTime? BeendetAm);
