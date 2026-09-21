using System.Globalization;

namespace NOOSE_Website.Services;

/// <summary>The one sentence a picker says about an absent agent.</summary>
/// <remarks>
/// Three pickers show this and none of them shares a component, so the wording lives here rather than three
/// times. It names the end day and nothing else - the roster tier of <see cref="AbsenceVisibility"/> grants a
/// colleague the row, never the reason, and a picker is the last place to widen that.
/// </remarks>
public static class AbsenceHint
{
    /// <summary>Hint for one agent, or empty when they are there on the reference day.</summary>
    public static string For(IReadOnlyDictionary<string, DateOnly>? absent, string agentId, DateOnly reference)
        => absent is not null && absent.TryGetValue(agentId, out var until)
            ? $"abgemeldet bis {Day(until, reference)}"
            : string.Empty;

    /// <summary>Sentence naming the agents that were picked although they are away.</summary>
    public static string Selected(
        IReadOnlyDictionary<string, DateOnly>? absent,
        IEnumerable<(string Id, string Name)> picked,
        DateOnly reference)
    {
        if (absent is null || absent.Count == 0)
        {
            return string.Empty;
        }
        var away = picked
            .Where(p => absent.ContainsKey(p.Id))
            .Select(p => $"{p.Name} ({Day(absent[p.Id], reference)})")
            .ToList();
        if (away.Count == 0)
        {
            return string.Empty;
        }
        return away.Count == 1
            ? $"Abgemeldet bis: {away[0]}"
            : $"Abgemeldet bis: {string.Join(", ", away)}";
    }

    // the year only earns its place when it differs from the day the picker is asking about
    private static string Day(DateOnly until, DateOnly reference)
        => until.Year == reference.Year
            ? until.ToString("dd.MM.", CultureInfo.GetCultureInfo("de-DE"))
            : until.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));
}
