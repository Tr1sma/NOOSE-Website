using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Vorgaenge;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einem Vorgang (beteiligte Ermittlungskraft). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// den Vorgang ist Cascade. Vorlage: <see cref="Operationen.OperationAgent"/>.
/// </summary>
public class VorgangAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string VorgangId { get; set; } = string.Empty;
    public Vorgang? Vorgang { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Markiert diesen zugeteilten Agent als Fallführer des Vorgangs (mehrere je Akte möglich).</summary>
    public bool IstFallfuehrer { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
