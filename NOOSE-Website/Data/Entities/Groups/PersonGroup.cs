using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Groups;

/// <summary>
/// Eine Personengruppe (loser Zusammenschluss von Personen) als Akte – Phase 4. Bündelt Mitglieder,
/// zugeteilte Agents und eine Einstufung mit Verlauf. Der Erfassungsfortschritt „x/y" ergibt sich aus
/// den erfassten Mitgliedern (x) gegenüber der geschätzten Gesamtgröße (y, <see cref="GeschaetzteMitgliederzahl"/>).
/// Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
[Table("Personengruppen")]
public class PersonGroup : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-G-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    [Column("Beschreibung")]
    public string? Description { get; set; }
    [Column("Ziele")]
    public string? Targets { get; set; }

    /// <summary>Kategorie der Gruppen-Akte (Persönlichkeit/Gruppierung/Person of Interest); Default Gruppierung.</summary>
    [Column("Art")]
    public GroupsKind Kind { get; set; } = GroupsKind.Grouping;

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Geschätzte Gesamtgröße der Gruppe (= y im Erfassungsfortschritt x/y); optional.</summary>
    [Column("GeschaetzteMitgliederzahl")]
    public int? EstimatedMemberCount { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    // ---- Kind-Tabellen ----
    public List<PersonGroupMember> Members { get; set; } = new();
    public List<PersonGroupAgent> Agents { get; set; } = new();

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
