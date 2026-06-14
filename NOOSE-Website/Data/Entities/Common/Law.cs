using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Ein Paragraf/eine Rechtsgrundlage der Wissensbasis (Gesetzbuch-Modul, Phase 7). Wird von der
/// Führung kuratiert, ist für alle aktiven Agenten lesbar und über die generische Verknüpfungs-Engine
/// (<see cref="Verknuepfung"/>) mit beliebigen Akten – insbesondere Vorgängen und Personen-Doks –
/// verknüpfbar. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Gesetze")]
public class Law : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gesetzbuch/Sammlung, z. B. „StGB", „StVO", „NOOSE-Dienstrecht".</summary>
    [Column("Gesetzbuch")]
    public string LawBook { get; set; } = string.Empty;

    /// <summary>Paragrafen-Bezeichnung, z. B. „§ 31" oder „Art. 2".</summary>
    [Column("Paragraf")]
    public string Paragraph { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Volltext des Paragrafen (Klartext).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optionales Strafmaß / Rechtsfolge als Freitext.</summary>
    [Column("Strafmass")]
    public string? Sentence { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
