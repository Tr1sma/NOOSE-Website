using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Groups;

/// <summary>A person group (loose collection of people) as a case file with members, assigned agents and classification.</summary>
[Table("Personengruppen")]
public class PersonGroup : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Human-readable unique case number (e.g. NOOSE-G-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    [Column("Beschreibung")]
    public string? Description { get; set; }
    [Column("Ziele")]
    public string? Targets { get; set; }

    [Column("Art")]
    public GroupsKind Kind { get; set; } = GroupsKind.Grouping;

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Estimated total group size (optional).</summary>
    [Column("GeschaetzteMitgliederzahl")]
    public int? EstimatedMemberCount { get; set; }

    /// <summary>Classified: visible only to leadership/admin.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    public List<PersonGroupMember> Members { get; set; } = new();
    public List<PersonGroupAgent> Agents { get; set; } = new();

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
