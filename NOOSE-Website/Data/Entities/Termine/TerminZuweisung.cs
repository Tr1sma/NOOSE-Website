using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Termine;

/// <summary>
/// Teilnehmer eines Termins (Zuteilung an einen NOOSE-Agent) – Phase 8 (Block C). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>;
/// FK auf den Termin ist Cascade. Vorlage: <see cref="Aufgaben.AufgabeZuweisung"/>.
/// </summary>
[Table("TerminZuweisungen")]
public class TerminZuweisung : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("TerminId")]
    public string TerminId { get; set; } = string.Empty;
    public Termin? Termin { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

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
