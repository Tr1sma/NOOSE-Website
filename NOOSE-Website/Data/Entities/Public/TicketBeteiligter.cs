using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>One agent attached to a ticket; the internal thread is readable to these and to leadership.</summary>
/// <remarks>
/// Shaped after <c>TaskforceAgent</c>: an assignment row, audited but never soft-deleted — removing someone from a
/// ticket removes them, it does not leave a tombstone that would still grant the read.
/// <para>
/// The read mark lives here rather than on the ticket, because the desk mark there is one for the whole house: with
/// several agents on one internal thread, the first to look would otherwise clear it for everybody.
/// </para>
/// </remarks>
[Table("TicketBeteiligte")]
public class TicketParticipant : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("TicketId")]
    public string TicketId { get; set; } = string.Empty;
    public Ticket? Ticket { get; set; }

    [Column("AgentId")]
    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>This participant's own read mark over the internal thread.</summary>
    [Column("ZuletztGelesenAm")]
    public DateTime? LastReadAt { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
