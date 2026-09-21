using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Radio;

/// <summary>One entry of the radio plan: a frequency and who talks on it.</summary>
/// <remarks>
/// Faction frequencies deliberately have no row here — they stay in <c>Faction.Radio</c> and the plan reads them
/// live. Two places to write the same frequency is two places to forget one of them.
/// </remarks>
[Table("Funkkanaele")]
public class RadioChannel : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Free text, because in-game frequencies are written by hand ("411.7").</summary>
    [Column("Frequenz")]
    public string Frequency { get; set; } = string.Empty;

    [Column("Bezeichnung")]
    public string Label { get; set; } = string.Empty;

    [Column("Bereich")]
    public RadioScope Scope { get; set; } = RadioScope.Noose;

    /// <summary>Set only while <see cref="Scope"/> is <see cref="RadioScope.Partner"/>.</summary>
    [Column("Partnerbehoerde")]
    public PartnerAgency? Agency { get; set; }

    /// <summary>Bound taskforce; a bound channel inherits that taskforce's visibility.</summary>
    [Column("TaskforceId")]
    public string? TaskforceId { get; set; }

    [Column("Bemerkung")]
    public string? Note { get; set; }

    /// <summary>Leadership only, both to see and to change.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

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
