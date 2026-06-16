using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>Doc template; holds default field values for pre-filling forms.</summary>
[Table("DokVorlagen")]
public class DocTemplate : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Unique template name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Active templates only.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    /// <summary>Sort order; ascending.</summary>
    [Column("Sortierung")]
    public int Sorting { get; set; }

    // ---- default field values ----
    [Column("StandardGrund")]
    public string? DefaultReason { get; set; }

    /// <summary>Default faction name.</summary>
    [Column("StandardFraktion")]
    public string? DefaultFaction { get; set; }

    [Column("StandardErhalteneInformationen")]
    public string? DefaultReceivedInformation { get; set; }

    [Column("StandardWahrheitsserum")]
    public bool DefaultTruthSerum { get; set; }

    [Column("StandardAusgang")]
    public MeasureOutcome DefaultOutcome { get; set; } = MeasureOutcome.RunningStill;

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
