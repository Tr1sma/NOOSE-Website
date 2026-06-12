using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Termine;

/// <summary>
/// Teilnehmer eines Termins (Zuteilung an einen NOOSE-Agent) – Phase 8 (Block C). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>;
/// FK auf den Termin ist Cascade. Vorlage: <see cref="Aufgaben.AufgabeZuweisung"/>.
/// </summary>
public class TerminZuweisung : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TerminId { get; set; } = string.Empty;
    public Termin? Termin { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
