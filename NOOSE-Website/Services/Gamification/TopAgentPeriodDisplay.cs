using System.Globalization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Wording of one award run: headline, note phrase, de-duplication marker and the matching board filter.</summary>
public sealed record TopAgentPeriodWording(string Headline, string NotePhrase, string Marker, string? PeriodQuery);

/// <summary>German wording for the top-agent announcement, derived from the configured interval.</summary>
public static class TopAgentPeriodDisplay
{
    /// <summary>Wording for an interval of <paramref name="intervalDays"/> days ending at <paramref name="localNow"/>.</summary>
    /// <remarks>
    /// The marker is always a substring of the note phrase: the note de-dup matches Text.Contains against the
    /// phrase this same code writes. Every marker but the weekly one carries the run day, because a calendar-only
    /// label lets two runs inside one interval share it and silently skip the second note.
    /// </remarks>
    public static TopAgentPeriodWording For(int intervalDays, DateTime localNow) => intervalDays switch
    {
        <= 1 => Dated("Tages", Day(localNow), null),
        7 => Weekly(localNow),
        >= 28 and <= 31 => Dated("Monats", $"bis {Day(localNow)}", nameof(GamificationPeriod.Month)),
        >= 90 and <= 92 => Dated("Quartals", $"bis {Day(localNow)}", null),
        >= 365 and <= 366 => Dated("Jahres", $"bis {Day(localNow)}", null),
        _ => Rolling(intervalDays, localNow),
    };

    private static TopAgentPeriodWording Dated(string genitive, string stamp, string? query)
    {
        var marker = $"{genitive} ({stamp})";
        return new TopAgentPeriodWording($"des {genitive}", $"des {marker}", marker, query);
    }

    private static TopAgentPeriodWording Weekly(DateTime localNow)
    {
        var week = ISOWeek.GetWeekOfYear(localNow);
        // unpadded on purpose: filed notes read this way, IsoWeekPeriod.Label pads and would stop matching
        var marker = string.Create(CultureInfo.InvariantCulture, $"KW {week}/{ISOWeek.GetYear(localNow)}");
        return new TopAgentPeriodWording(
            $"der Woche (KW {week})", $"der Woche {marker}", marker, nameof(GamificationPeriod.Week));
    }

    private static TopAgentPeriodWording Rolling(int days, DateTime localNow)
    {
        // leads with a word, not a digit: "4 Tage (bis ...)" is a substring of "14 Tage (bis ...)"
        var marker = $"letzten {days} Tage (bis {Day(localNow)})";
        return new TopAgentPeriodWording($"der letzten {days} Tage", $"der {marker}", marker, null);
    }

    // '.' is a literal in a custom format string; '/' would be swapped for the de-DE date separator
    private static string Day(DateTime value) => value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
