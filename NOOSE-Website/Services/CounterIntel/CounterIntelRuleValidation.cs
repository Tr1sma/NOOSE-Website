using NOOSE_Website.Models.CounterIntel;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Rule invariants, checked in the service so a hand-crafted payload cannot slip past the editor.</summary>
public static class CounterIntelRuleValidation
{
    public static void Validate(CounterIntelRuleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new InvalidOperationException("Die Regel braucht einen Namen.");
        }
        if (input.Name.Trim().Length > 150)
        {
            throw new InvalidOperationException("Der Regelname darf höchstens 150 Zeichen lang sein.");
        }
        Validate(input.Definition);
    }

    public static void Validate(CounterIntelRuleDefinition definition)
    {
        if (definition.WindowDays is < 1 or > CounterIntelRuleDefinition.MaxWindowDays)
        {
            throw new InvalidOperationException(
                $"Der Zeitraum muss zwischen 1 und {CounterIntelRuleDefinition.MaxWindowDays} Tagen liegen.");
        }
        if (definition.Threshold < 1)
        {
            throw new InvalidOperationException("Die Schwelle muss mindestens 1 betragen.");
        }
        if (definition.FromHour is < 0 or > 23 || definition.ToHour is < 0 or > 23)
        {
            throw new InvalidOperationException("Die Uhrzeiten müssen zwischen 0 und 23 liegen.");
        }
        if (definition.Bucket == CounterIntelBucket.Sliding
            && definition.SlidingMinutes is < 1 or > CounterIntelRuleDefinition.MaxSlidingMinutes)
        {
            throw new InvalidOperationException(
                $"Das gleitende Fenster muss zwischen 1 und {CounterIntelRuleDefinition.MaxSlidingMinutes} Minuten liegen.");
        }
        // an all-day rule that also lists every weekday is fine; listing none of them is a rule that can never fire
        if (definition.Weekdays.Count > 7)
        {
            throw new InvalidOperationException("Es gibt nur sieben Wochentage.");
        }
    }
}
