using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Parteien;

/// <summary>
/// Eine Partei (politische Organisation) als vollwertige Akte – Phase 5a. Bündelt Stammdaten, ihre
/// Mitglieder (Personen, mit Rolle/Leitung) und zugeteilte Special Agents sowie eine Einstufung mit
/// Verlauf. Die Leitung ist kein eigenes Feld, sondern wird über das Mitglieds-Flag <c>IstLeitung</c>
/// abgebildet (Parität zu Fraktion/Gruppe). Voll auditiert und papierkorbfähig (<see cref="IAuditable"/>
/// + <see cref="ISoftDelete"/>). Konflikte/Bündnisse zu anderen Organisationen laufen über die generische
/// Verknüpfungs-Engine.
/// </summary>
[Table("Parteien")]
public class Partei : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-PT-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }
    [Column("Ziele")]
    public string? Ziele { get; set; }

    /// <summary>Interne Bemerkungen/Vermerke zur Partei (Freitext, getrennt von Beschreibung/Zielen).</summary>
    [Column("Bemerkungen")]
    public string? Bemerkungen { get; set; }

    [Column("Einstufung")]
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<ParteiMitglied> Mitglieder { get; set; } = new();
    public List<ParteiAgent> Agenten { get; set; } = new();

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
