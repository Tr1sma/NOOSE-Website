using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personal;

/// <summary>
/// Ein Beförderungsantrag für einen Agent (Phase 5e). Die Führung (Supervisory+) schlägt einen
/// <see cref="ZielDienstgrad"/> vor; Deputy Director+/Admin entscheidet. Bei Genehmigung wird der Rang des
/// Agents gesetzt und im Dienstgrad-Verlauf protokolliert (siehe <c>AgentVerwaltungService</c>).
/// </summary>
[Table("AgentBefoerderungsantraege")]
public class AgentBefoerderungsantrag : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent, der befördert werden soll.</summary>
    public string AgentId { get; set; } = string.Empty;

    [Column("ZielDienstgrad")]
    public Dienstgrad ZielDienstgrad { get; set; }

    [Column("Begruendung")]
    public string? Begruendung { get; set; }

    public BefoerderungStatus Status { get; set; } = BefoerderungStatus.Beantragt;

    /// <summary>Codename des Antragstellers (denormalisiert).</summary>
    [Column("AntragstellerName")]
    public string? AntragstellerName { get; set; }

    /// <summary>Codename des Entscheiders (denormalisiert), null solange offen.</summary>
    [Column("EntscheiderName")]
    public string? EntscheiderName { get; set; }

    [Column("EntschiedenAm")]
    public DateTime? EntschiedenAm { get; set; }

    [Column("Entscheidungsnotiz")]
    public string? Entscheidungsnotiz { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
