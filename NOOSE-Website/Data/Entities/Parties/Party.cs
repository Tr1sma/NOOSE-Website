using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Parties;

/// <summary>
/// Eine Partei (politische Organisation) als vollwertige Akte – Phase 5a. Bündelt Stammdaten, ihre
/// Mitglieder (Personen, mit Rolle/Leitung) und zugeteilte Special Agents sowie eine Einstufung mit
/// Verlauf. Die Leitung ist kein eigenes Feld, sondern wird über das Mitglieds-Flag <c>IstLeitung</c>
/// abgebildet (Parität zu Fraktion/Gruppe). Voll auditiert und papierkorbfähig (<see cref="IAuditable"/>
/// + <see cref="ISoftDelete"/>). Konflikte/Bündnisse zu anderen Organisationen laufen über die generische
/// Verknüpfungs-Engine.
/// </summary>
[Table("Parteien")]
public class Party : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-PT-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    [Column("Beschreibung")]
    public string? Description { get; set; }
    [Column("Ziele")]
    public string? Targets { get; set; }

    /// <summary>Interne Bemerkungen/Vermerke zur Partei (Freitext, getrennt von Beschreibung/Zielen).</summary>
    [Column("Bemerkungen")]
    public string? Remarks { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    // ---- Kind-Tabellen ----
    public List<PartyMember> Members { get; set; } = new();
    public List<PartyAgent> Agents { get; set; } = new();

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
