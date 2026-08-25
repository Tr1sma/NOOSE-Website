using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>One share of the bounty on a head: agency money from a cash account, or an agent's own money.</summary>
/// <remarks>
/// The outside only ever learns the sum of the advertised shares. Origin, donor, per-share amount, account, booking
/// and count stay inside — <c>PublicVisibility</c> says so and <c>PublicWantedModelTests</c> holds the outward record
/// to one number.
/// <para>
/// Deliberately not <see cref="ISoftDelete"/>: money history is append-only. A share is withdrawn by status, never
/// deleted, so there is nothing for the trash to hold and no filter that could hide a payment trail.
/// </para>
/// </remarks>
[Table("FahndungKopfgeldAnteile")]
public class FahndungKopfgeldAnteil : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FahndungId")]
    public string WantedId { get; set; } = string.Empty;
    public OeffentlicheFahndung? Wanted { get; set; }

    [Column("Herkunft")]
    public BountyOrigin Origin { get; set; }

    [Column("Betrag")]
    public decimal Amount { get; set; }

    /// <summary>Which cash book this share touches: paid from it when official, paid into it when private.</summary>
    /// <remarks>Null while a private share is only pledged — nothing has moved yet.</remarks>
    [Column("Konto")]
    public KassenKonto? Account { get; set; }

    /// <summary>Agent behind the share; for an official one the agent who committed the agency.</summary>
    [Column("StifterAgentId")]
    public string? DonorAgentId { get; set; }
    public Agent? DonorAgent { get; set; }

    /// <summary>Set when a private share was actually handed in; unique, so one booking backs at most one share.</summary>
    [Column("KassenBuchungId")]
    public string? KassenBuchungId { get; set; }

    [Column("Status")]
    public BountyShareStatus Status { get; set; } = BountyShareStatus.Zugesagt;

    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    /// <summary>Why the share was taken back; the service refuses a withdrawal without one.</summary>
    [Column("ZurueckgezogenGrund")]
    public string? WithdrawnReason { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
