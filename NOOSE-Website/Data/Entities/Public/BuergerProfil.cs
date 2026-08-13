using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>Public-area account of a citizen; the Discord login lives on the identity user, the RP name here.</summary>
/// <remarks>
/// The name is deliberately not <see cref="Agent.RealName"/>: that field is the agency's secret clear name behind
/// a leadership-only gate, while a citizen's name is what the public area shows them by. One field, two meanings
/// would make every real-name gate ambiguous.
/// </remarks>
[Table("BuergerProfile")]
public class BuergerProfil : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identity user (the citizen's Discord account, status Civilian).</summary>
    [Column("BenutzerId")]
    public string UserId { get; set; } = string.Empty;
    public Agent? User { get; set; }

    [Column("Vorname")]
    public string FirstName { get; set; } = string.Empty;

    [Column("Nachname")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Blocked citizens keep reading the public area but may submit nothing.</summary>
    [Column("IstGesperrt")]
    public bool IsBlocked { get; set; }
    [Column("SperrGrund")]
    public string? BlockedReason { get; set; }
    [Column("GesperrtVonId")]
    public string? BlockedById { get; set; }
    [Column("GesperrtAm")]
    public DateTime? BlockedAt { get; set; }

    /// <summary>Confirmed tips; the trust tier that raises the submission quota builds on this.</summary>
    [Column("BestaetigteHinweise")]
    public int ConfirmedTips { get; set; }

    /// <summary>Optional link to the person file this citizen belongs to.</summary>
    [Column("PersonId")]
    public string? LinkedPersonId { get; set; }

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
