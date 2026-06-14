using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Fraktion (wer bearbeitet die Fraktion). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Fraktion ist Cascade. Das Flag <see cref="IstErmittlungsleiter"/> markiert leitende Agents –
/// mehrere gleichzeitig möglich; gesetzt/entfernt nur durch die Führung.
/// </summary>
[Table("FraktionAgenten")]
public class FraktionAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }

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
