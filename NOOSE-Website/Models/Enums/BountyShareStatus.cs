using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Lifecycle state of one share of a bounty; decides whether it counts towards the advertised total.</summary>
/// <remarks>
/// Only <see cref="Zugesagt"/> and <see cref="Gesichert"/> are money on the head. <see cref="Beantragt"/> is an open
/// internal decision and must stay invisible outside, <see cref="Ausgezahlt"/> is spent, <see cref="Zurueckgezogen"/>
/// is gone. Nothing in this phase sets <see cref="Ausgezahlt"/> — paying an informant arrives with the reward phase;
/// the value exists from the start so the sum excludes it from the first line of code.
/// </remarks>
public enum BountyShareStatus
{
    Beantragt = 0,
    Zugesagt = 1,
    Gesichert = 2,
    Ausgezahlt = 3,
    Zurueckgezogen = 4,
}

/// <summary>Display labels and chip colours per share state.</summary>
public static class BountyShareStatusDisplay
{
    public static string Name(BountyShareStatus status) => status switch
    {
        BountyShareStatus.Beantragt => "Beantragt",
        BountyShareStatus.Zugesagt => "Zugesagt",
        BountyShareStatus.Gesichert => "Gesichert",
        BountyShareStatus.Ausgezahlt => "Ausgezahlt",
        BountyShareStatus.Zurueckgezogen => "Zurückgezogen",
        _ => "—",
    };

    public static Color ChipColor(BountyShareStatus status) => status switch
    {
        BountyShareStatus.Beantragt => Color.Info,
        BountyShareStatus.Zugesagt => Color.Warning,
        BountyShareStatus.Gesichert => Color.Success,
        BountyShareStatus.Ausgezahlt => Color.Default,
        BountyShareStatus.Zurueckgezogen => Color.Error,
        _ => Color.Default,
    };

    // which states count towards the advertised sum is not decided here: BountyShares holds that rule, because it
    // has to exist as an EF predicate as well and two spellings of one rule drift

    public static readonly IReadOnlyList<BountyShareStatus> All = new[]
    {
        BountyShareStatus.Beantragt,
        BountyShareStatus.Zugesagt,
        BountyShareStatus.Gesichert,
        BountyShareStatus.Ausgezahlt,
        BountyShareStatus.Zurueckgezogen,
    };
}
