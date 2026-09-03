using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>The recruitment ban rules: how long a rejection bans, what counts as active, how a picked date becomes UTC. Sole source of all three.</summary>
public static class BewerbungssperreRules
{
    /// <summary>Cooling-off period a rejection, a closure or a failed security check imposes.</summary>
    public static readonly TimeSpan BanDuration = TimeSpan.FromDays(14);

    /// <summary>End of the ban imposed right now.</summary>
    public static DateTime BannedUntil(DateTime nowUtc) => nowUtc + BanDuration;

    /// <summary>Active = permanent blacklist, or a temporary ban whose end is still ahead. There is no IsActive column.</summary>
    /// <remarks>
    /// One expression for every reader: the predicate used to stand copied in five places, and a reader that
    /// forgets it (the search provider did) offers rows the managing list no longer holds.
    /// </remarks>
    public static Expression<Func<Bewerbungssperre, bool>> Active(DateTime nowUtc)
        => s => s.IsBlacklist || s.BannedUntil > nowUtc;

    /// <summary>Turn a MudDatePicker value into the UTC end of that local day.</summary>
    /// <remarks>
    /// The picker hands out a local midnight with Kind Unspecified while GesperrtBis is UTC. Storing it raw
    /// shifted every "Anpassen" by the offset, and picking today ended the ban at 00:00 — already past.
    /// </remarks>
    public static DateTime PickedDateToUtc(DateTime picked)
        => DateTime.SpecifyKind(picked.Date, DateTimeKind.Local).AddDays(1).AddTicks(-1).ToUniversalTime();
}
