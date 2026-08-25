using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Public;

/// <summary>One paid slice of a bounty: what one tip earned out of one share.</summary>
/// <remarks>
/// A tip may draw from more than one share, so the receipt number is shared by the rows of one tip rather than being
/// unique per row — the citizen gets one receipt per tip, and the rows behind it are the accounting.
/// <para>
/// Deliberately not <see cref="ISoftDelete"/>, same reason as <see cref="FahndungKopfgeldAnteil"/>: money history is
/// append-only, and a soft-delete filter could hide a payment trail the receipt is the proof of. A wrong payout is
/// corrected by a counter-booking in the cash book.
/// </para>
/// </remarks>
[Table("HinweisBelohnungen")]
public class HinweisBelohnung : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>What the citizen quotes; minted per tip and per payout, so one tip's rows share it.</summary>
    [Column("BelegNummer")]
    public string ReceiptNumber { get; set; } = string.Empty;

    [Column("HinweisId")]
    public string TipId { get; set; } = string.Empty;
    public Hinweis? Tip { get; set; }

    [Column("AnteilId")]
    public string ShareId { get; set; } = string.Empty;
    public FahndungKopfgeldAnteil? Share { get; set; }

    [Column("Betrag")]
    public decimal Amount { get; set; }

    /// <summary>The withdrawal that moved this money; unique, so one booking backs at most one slice.</summary>
    /// <remarks>Null for a private share that never reached the till — the agent hands his own money over.</remarks>
    [Column("KassenBuchungId")]
    public string? KassenBuchungId { get; set; }

    /// <summary>Set instead of a booking when the donor paid the citizen directly.</summary>
    [Column("SelbstAusgezahltAm")]
    public DateTime? SelfPaidAt { get; set; }

    /// <summary>When the reward was paid; the receipt needs a date that is not an audit stamp.</summary>
    [Column("AusgezahltAm")]
    public DateTime PaidAt { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
