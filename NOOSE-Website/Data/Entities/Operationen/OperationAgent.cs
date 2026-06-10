using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Operationen;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Operation (beteiligte Einsatzkraft). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Operation ist Cascade.
/// </summary>
public class OperationAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string OperationId { get; set; } = string.Empty;
    public Operation? Operation { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Markiert diesen zugeteilten Agent als Ermittlungsleiter der Operation (mehrere je Akte möglich).</summary>
    public bool IstErmittlungsleiter { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
