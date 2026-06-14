using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Parteien;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Partei (wer bearbeitet die Partei). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Partei ist Cascade.
/// </summary>
[Table("ParteiAgenten")]
public class ParteiAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("ParteiId")]
    public string ParteiId { get; set; } = string.Empty;
    public Partei? Partei { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Markiert diesen zugeteilten Agent als Ermittlungsleiter der Akte (mehrere je Akte möglich).</summary>
    [Column("IstErmittlungsleiter")]
    public bool IstErmittlungsleiter { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }
}
