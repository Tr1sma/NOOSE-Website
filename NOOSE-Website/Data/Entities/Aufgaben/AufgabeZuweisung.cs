using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Aufgaben;

/// <summary>
/// Zuweisung einer Aufgabe an einen NOOSE-Agent – Phase 6. Join-Entity mit <see cref="IAuditable"/> (flache
/// Zuweisung, keine Leitungs-Markierung). FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>;
/// FK auf die Aufgabe ist Cascade. Vorlage: <see cref="Vorgaenge.VorgangAgent"/>.
/// </summary>
[Table("AufgabeZuweisungen")]
public class AufgabeZuweisung : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("AufgabeId")]
    public string AufgabeId { get; set; } = string.Empty;
    public Aufgabe? Aufgabe { get; set; }

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
