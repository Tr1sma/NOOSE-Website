using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Ankuendigungen;

/// <summary>
/// Quittierung (Lesebestätigung) eines Agenten zu einer Ankündigung – Phase 6. Join-Entity mit
/// <see cref="IAuditable"/> (kein Soft-Delete – Muster <see cref="Aufgaben.AufgabeZuweisung"/>). Die Zeilen werden
/// beim Anlegen einer Ankündigung mit <c>QuittierungVerlangt</c> als Empfänger-Snapshot erzeugt; <see cref="QuittiertAm"/>
/// bleibt null, bis der Agent „Kenntnis nimmt". FK auf den <see cref="Agent"/> ist <c>Restrict</c>, FK auf die
/// Ankündigung ist Cascade.
/// </summary>
public class AnkuendigungQuittierung : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AnkuendigungId { get; set; } = string.Empty;
    public Ankuendigung? Ankuendigung { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Zeitpunkt der Kenntnisnahme; null = noch offen.</summary>
    public DateTime? QuittiertAm { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
