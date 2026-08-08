using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Whether an evidence-room entry stores an item in or takes it out.</summary>
public enum EvidenceEntryType
{
    Deposit = 0,
    Withdrawal = 1,
}

/// <summary>Display labels, icons and chip colors.</summary>
public static class EvidenceEntryTypeDisplay
{
    public static string Name(EvidenceEntryType type) => type switch
    {
        EvidenceEntryType.Deposit => "Einlagerung",
        EvidenceEntryType.Withdrawal => "Herausnahme",
        _ => "—",
    };

    public static string Icon(EvidenceEntryType type) => type switch
    {
        EvidenceEntryType.Deposit => Icons.Material.Filled.MoveToInbox,
        EvidenceEntryType.Withdrawal => Icons.Material.Filled.Outbox,
        _ => Icons.Material.Filled.Inventory2,
    };

    public static Color ChipColor(EvidenceEntryType type) => type switch
    {
        EvidenceEntryType.Deposit => Color.Success,
        EvidenceEntryType.Withdrawal => Color.Warning,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<EvidenceEntryType> All = new[]
    {
        EvidenceEntryType.Deposit,
        EvidenceEntryType.Withdrawal,
    };
}
