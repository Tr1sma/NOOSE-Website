using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>Recruitment ban or blacklist for one applicant; reversible via soft-delete.</summary>
[Table("Bewerbungssperren")]
public class Bewerbungssperre : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("BewerberId")]
    public string AgentId { get; set; } = string.Empty;

    [Column("DiscordId")]
    public string? DiscordId { get; set; }

    [Column("BewerberName")]
    public string? ApplicantName { get; set; }

    [Column("BewerbungId")]
    public string? BewerbungId { get; set; }

    /// <summary>false = temporary ban until BannedUntil; true = permanent blacklist.</summary>
    [Column("IstBlacklist")]
    public bool IsBlacklist { get; set; }

    [Column("GesperrtBis")]
    public DateTime? BannedUntil { get; set; }

    [Column("Grund")]
    public string? Reason { get; set; }

    [Column("ErstelltVonName")]
    public string? CreatedByName { get; set; }

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
