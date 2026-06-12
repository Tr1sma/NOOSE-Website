using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein Paragraf/eine Rechtsgrundlage der Wissensbasis (Gesetzbuch-Modul, Phase 7). Wird von der
/// Führung kuratiert, ist für alle aktiven Agenten lesbar und über die generische Verknüpfungs-Engine
/// (<see cref="Verknuepfung"/>) mit beliebigen Akten – insbesondere Vorgängen und Personen-Doks –
/// verknüpfbar. Voll auditiert und papierkorbfähig.
/// </summary>
public class Gesetz : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gesetzbuch/Sammlung, z. B. „StGB", „StVO", „NOOSE-Dienstrecht".</summary>
    public string Gesetzbuch { get; set; } = string.Empty;

    /// <summary>Paragrafen-Bezeichnung, z. B. „§ 31" oder „Art. 2".</summary>
    public string Paragraf { get; set; } = string.Empty;

    public string Titel { get; set; } = string.Empty;

    /// <summary>Volltext des Paragrafen (Klartext).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Optionales Strafmaß / Rechtsfolge als Freitext.</summary>
    public string? Strafmass { get; set; }

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
