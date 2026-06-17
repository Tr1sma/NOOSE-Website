using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>Central person record; fully audited and soft-deletable.</summary>
[Table("Personen")]
public class Person : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Lebensstatus")]
    public LifeStatus LifeStatus { get; set; } = LifeStatus.Alive;

    /// <summary>Dead until (UTC).</summary>
    [Column("TotBis")]
    public DateTime? DeadUntil { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Classified; leadership/admin only.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    /// <summary>Threat score (0–100).</summary>
    [Column("BedrohungsScore")]
    public int? ThreatScore { get; set; }

    /// <summary>Threat confidence (0–100).</summary>
    [Column("BedrohungsKonfidenz")]
    public int? ThreatConfidence { get; set; }

    [Column("BedrohungsDetailJson")]
    public string? ThreatDetailJson { get; set; }

    /// <summary>Last score calculation (UTC).</summary>
    [Column("ScoreBerechnetAm")]
    public DateTime? ScoreCalculatedAt { get; set; }

    public List<PersonAlias> Aliases { get; set; } = new();
    public List<PersonPhone> PhoneNumbers { get; set; } = new();
    public List<PersonVehicle> Vehicles { get; set; } = new();
    public List<PersonLocation> Locations { get; set; } = new();
    public List<PersonWeapon> Weapons { get; set; } = new();
    public List<PersonPhoto> Photos { get; set; } = new();
    public List<PersonDoc> Docs { get; set; } = new();
    public List<Observation> Observations { get; set; } = new();

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

    [NotMapped]
    public LifeStatus EffectiveLifeStatus
        => LifeStatusLogic.Effective(LifeStatus, DeadUntil, DateTime.UtcNow);
}
