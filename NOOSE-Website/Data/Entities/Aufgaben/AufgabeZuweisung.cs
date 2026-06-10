using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Aufgaben;

/// <summary>
/// Zuweisung einer Aufgabe an einen NOOSE-Agent – Phase 6. Join-Entity mit <see cref="IAuditable"/> (flache
/// Zuweisung, keine Leitungs-Markierung). FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>;
/// FK auf die Aufgabe ist Cascade. Vorlage: <see cref="Vorgaenge.VorgangAgent"/>.
/// </summary>
public class AufgabeZuweisung : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AufgabeId { get; set; } = string.Empty;
    public Aufgabe? Aufgabe { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
