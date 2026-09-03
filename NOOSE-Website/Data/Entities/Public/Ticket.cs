using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>A citizen's concern addressed to leadership; the replacement for the public Discord ticket.</summary>
/// <remarks>
/// A ticket hangs off no record — it is correspondence, not case material — which is why it appears on no timeline and
/// resolves to no parent in the chronicle. Prefix <c>T</c>.
/// <para>
/// <see cref="LastActivityAt"/> is the sort key of the desk and is written along with the status change that caused it.
/// The two read marks are not: reading is not a change, so they go through <c>ExecuteUpdate</c>.
/// </para>
/// </remarks>
[Table("Tickets")]
public class Ticket : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Art")]
    public TicketArt Kind { get; set; } = TicketArt.Fuehrungsebene;

    /// <summary>Null for an internal ticket; an agent's own concern has no citizen behind it.</summary>
    [Column("BuergerProfilId")]
    public string? CitizenProfileId { get; set; }
    public BuergerProfil? CitizenProfile { get; set; }

    /// <summary>Set for an internal ticket: who opened it. A citizen ticket leaves it null.</summary>
    [Column("EroeffnetVonAgentId")]
    public string? OpenedByAgentId { get; set; }
    public Agent? OpenedByAgent { get; set; }

    [Column("Betreff")]
    public string Subject { get; set; } = string.Empty;

    [Column("Status")]
    public TicketStatus Status { get; set; } = TicketStatus.Offen;

    [Column("BearbeiterId")]
    public string? HandlerId { get; set; }
    public Agent? Handler { get; set; }

    [Column("LetzteAktivitaetAm")]
    public DateTime LastActivityAt { get; set; }

    /// <summary>Drives the citizen's unread count; only the citizen's own reading moves it.</summary>
    [Column("ZuletztGelesenBuergerAm")]
    public DateTime? CitizenLastReadAt { get; set; }

    /// <summary>Drives the desk's unread marker; only a handler opening the ticket moves it.</summary>
    [Column("ZuletztGelesenAgentAm")]
    public DateTime? AgentLastReadAt { get; set; }

    [Column("GeschlossenAm")]
    public DateTime? ClosedAt { get; set; }

    /// <summary>Who closed it; this is the only place that name is kept.</summary>
    [Column("GeschlossenVonId")]
    public string? ClosedById { get; set; }

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
