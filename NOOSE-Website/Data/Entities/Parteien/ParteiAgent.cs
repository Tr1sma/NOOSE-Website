using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Parteien;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Partei (wer bearbeitet die Partei). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Partei ist Cascade.
/// </summary>
public class ParteiAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ParteiId { get; set; } = string.Empty;
    public Partei? Partei { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
