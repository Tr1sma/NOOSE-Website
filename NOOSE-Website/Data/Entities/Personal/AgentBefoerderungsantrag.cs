using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personal;

/// <summary>
/// Ein Beförderungsantrag für einen Agent (Phase 5e). Die Führung (Supervisory+) schlägt einen
/// <see cref="ZielDienstgrad"/> vor; Deputy Director+/Admin entscheidet. Bei Genehmigung wird der Rang des
/// Agents gesetzt und im Dienstgrad-Verlauf protokolliert (siehe <c>AgentVerwaltungService</c>).
/// </summary>
public class AgentBefoerderungsantrag : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent, der befördert werden soll.</summary>
    public string AgentId { get; set; } = string.Empty;

    public Dienstgrad ZielDienstgrad { get; set; }

    public string? Begruendung { get; set; }

    public BefoerderungStatus Status { get; set; } = BefoerderungStatus.Beantragt;

    /// <summary>Codename des Antragstellers (denormalisiert).</summary>
    public string? AntragstellerName { get; set; }

    /// <summary>Codename des Entscheiders (denormalisiert), null solange offen.</summary>
    public string? EntscheiderName { get; set; }

    public DateTime? EntschiedenAm { get; set; }

    public string? Entscheidungsnotiz { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
