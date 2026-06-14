using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein Paragraf/eine Rechtsgrundlage der Wissensbasis (Gesetzbuch-Modul, Phase 7). Wird von der
/// Führung kuratiert, ist für alle aktiven Agenten lesbar und über die generische Verknüpfungs-Engine
/// (<see cref="Verknuepfung"/>) mit beliebigen Akten – insbesondere Vorgängen und Personen-Doks –
/// verknüpfbar. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Gesetze")]
public class Gesetz : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gesetzbuch/Sammlung, z. B. „StGB", „StVO", „NOOSE-Dienstrecht".</summary>
    [Column("Gesetzbuch")]
    public string Gesetzbuch { get; set; } = string.Empty;

    /// <summary>Paragrafen-Bezeichnung, z. B. „§ 31" oder „Art. 2".</summary>
    [Column("Paragraf")]
    public string Paragraf { get; set; } = string.Empty;

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Volltext des Paragrafen (Klartext).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optionales Strafmaß / Rechtsfolge als Freitext.</summary>
    [Column("Strafmass")]
    public string? Strafmass { get; set; }

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
