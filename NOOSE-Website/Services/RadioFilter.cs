using NOOSE_Website.Models.Radio;

namespace NOOSE_Website.Services;

/// <summary>The radio plan's own search box, as a predicate rather than a page detail.</summary>
/// <remarks>
/// It lives here because the page has no test of its own - there is no bUnit in this repo - and the one thing
/// the plan exists for is that this filter reaches every block. The two overloads differ on purpose: a channel
/// frequency is normalised when it is written, a faction frequency is typed into the faction record by hand and
/// is not, so the stored value may itself carry a comma.
/// </remarks>
public static class RadioFilter
{
    public static bool Matches(RadioChannelRow row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }
        var needle = search.Trim();
        return Has(row.Frequency, RadioFrequency.Normalize(needle))
            || Has(row.Label, needle)
            || Has(row.Note, needle)
            || Has(row.TaskforceName, needle);
    }

    public static bool Matches(RadioFactionRow row, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }
        var needle = search.Trim();
        // both spellings of the query, because nothing normalised the stored value
        return Has(row.Frequency, RadioFrequency.Normalize(needle))
            || Has(row.Frequency, RadioFrequency.Comma(needle))
            || Has(row.Name, needle);
    }

    private static bool Has(string? haystack, string needle)
        => haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
