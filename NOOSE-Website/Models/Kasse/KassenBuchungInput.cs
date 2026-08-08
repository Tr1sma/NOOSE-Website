using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Kasse;

/// <summary>Form model for creating/editing a cash booking.</summary>
public class KassenBuchungInput
{
    public KassenKonto Account { get; set; } = KassenKonto.Schwarzgeld;
    public KassenBuchungArt Kind { get; set; } = KassenBuchungArt.Einzahlung;

    /// <summary>Magnitude for deposit/withdrawal; the target balance for a correction.</summary>
    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    /// <summary>Booking agent; defaults to the actor when empty.</summary>
    public string? BookedById { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
