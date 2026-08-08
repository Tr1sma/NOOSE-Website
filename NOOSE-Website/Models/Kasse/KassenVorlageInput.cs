using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Kasse;

/// <summary>Input model for a recurring-booking template.</summary>
public class KassenVorlageInput
{
    public string Name { get; set; } = string.Empty;
    public KassenKonto Account { get; set; } = KassenKonto.Schwarzgeld;
    public KassenBuchungArt Kind { get; set; } = KassenBuchungArt.Einzahlung;
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }
}
