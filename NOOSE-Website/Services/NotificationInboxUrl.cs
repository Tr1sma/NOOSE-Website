using System.Globalization;
using NOOSE_Website.Components.Common.Shared;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Notifications;

namespace NOOSE_Website.Services;

/// <summary>The inbox filter in the address: reading, writing and turning local days into a UTC window.</summary>
/// <remarks>
/// The page number is deliberately not part of it. A saved view keeps exactly these pairs, and a view that
/// remembered "page 3" would open somewhere else tomorrow.
/// </remarks>
public static class NotificationInboxUrl
{
    public const string TypesKey = "arten";
    public const string FromKey = "von";
    public const string ToKey = "bis";
    public const string UnreadKey = "ungelesen";

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>Reads the filter out of a query string, with or without its leading "?"; anything unknown drops out.</summary>
    public static NotificationInboxFilter Parse(string? query)
    {
        var types = (QueryState.Read(query, TypesKey) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseType)
            .OfType<NotificationType>()
            .Distinct()
            .Order()
            .ToList();
        return new NotificationInboxFilter(types, ParseDay(QueryState.Read(query, FromKey)),
            ParseDay(QueryState.Read(query, ToKey)), QueryState.ReadFlag(query, UnreadKey));
    }

    /// <summary>The filter as query pairs; unset parts are empty and drop out of the address.</summary>
    public static (string Name, string? Value)[] ToPairs(NotificationInboxFilter filter) =>
    [
        (TypesKey, filter.Types.Count == 0 ? null : string.Join(",", filter.Types.Distinct().Order())),
        (FromKey, filter.From?.ToString(DayFormat, CultureInfo.InvariantCulture)),
        (ToKey, filter.To?.ToString(DayFormat, CultureInfo.InvariantCulture)),
        (UnreadKey, filter.OnlyUnread ? "1" : null),
    ];

    /// <summary>Local calendar days to a half-open UTC window; the last day counts whole.</summary>
    public static (DateTime? FromUtc, DateTime? ToUtc) ToUtcRange(DateTime? fromDay, DateTime? toDay)
    {
        // picked the wrong way round: the days between them are still what was meant
        if (fromDay is { } a && toDay is { } b && a.Date > b.Date)
        {
            (fromDay, toDay) = (toDay, fromDay);
        }
        return (fromDay is { } from ? LocalMidnightToUtc(from.Date) : null,
            // the calendar's last day has no next midnight; a typed address must not throw
            toDay is { } to && to.Date < DateTime.MaxValue.Date ? LocalMidnightToUtc(to.Date.AddDays(1)) : null);
    }

    private static DateTime LocalMidnightToUtc(DateTime day)
        => DateTime.SpecifyKind(day, DateTimeKind.Local).ToUniversalTime();

    // names only: a number would reach types that are not members
    private static NotificationType? ParseType(string raw)
        => !int.TryParse(raw, out _) && Enum.TryParse<NotificationType>(raw, ignoreCase: true, out var type)
           && Enum.IsDefined(type)
            ? type
            : null;

    private static DateTime? ParseDay(string? raw)
        => DateTime.TryParseExact(raw, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day.Date
            : null;
}
