using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>When the viewer last visited a record, and what others entered on it since.</summary>
public static class RecordVisits
{
    /// <summary>Views closer together than this count as one visit.</summary>
    public static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(30);

    /// <summary>Own views read to find the previous visit; a longer unbroken chain yields none.</summary>
    public const int LookBack = 100;

    /// <summary>Parts named in the summary before the rest folds into "und mehr".</summary>
    public const int MaxParts = 6;

    // heaviest first: what changes how the file reads leads the line
    private static readonly TimelineCategory[] Weight =
    [
        TimelineCategory.Classification,
        TimelineCategory.Doc,
        TimelineCategory.Observation,
        TimelineCategory.Comment,
        TimelineCategory.Relation,
        TimelineCategory.Membership,
        TimelineCategory.Link,
        TimelineCategory.Source,
        TimelineCategory.Photo,
        TimelineCategory.Activity,
        TimelineCategory.Followup,
        TimelineCategory.Allocation,
        TimelineCategory.Agenda,
        TimelineCategory.Attendance,
        TimelineCategory.SignOff,
        TimelineCategory.Change,
        TimelineCategory.Asset,
        TimelineCategory.Deletion,
        TimelineCategory.Restoration,
        TimelineCategory.ThreatScore,
    ];

    /// <summary>Last view of the previous visit, or null when every view belongs to the current one.</summary>
    public static DateTime? PreviousSessionEnd(IEnumerable<DateTime> viewsNewestFirst, DateTime nowUtc)
    {
        var boundary = nowUtc;
        foreach (var view in viewsNewestFirst)
        {
            if (boundary - view >= SessionGap)
            {
                return view;
            }
            // a view stamped ahead of now (clock skew) still belongs to this visit
            if (view < boundary)
            {
                boundary = view;
            }
        }
        return null;
    }

    /// <summary>Entered after the visit, and not by the viewer.</summary>
    public static bool IsNew(TimelineEntry entry, DateTime? sinceUtc, string? viewerId)
        => sinceUtc is { } since
            && (entry.RecordedAt ?? entry.Timestamp) > since
            && (string.IsNullOrEmpty(viewerId) || entry.ActorId != viewerId);

    /// <summary>What is new, heaviest first, as one German phrase; empty when nothing is.</summary>
    public static string Summarise(IEnumerable<TimelineEntry> fresh)
    {
        var parts = fresh
            .GroupBy(e => e.Category)
            .OrderBy(g => Rank(g.Key))
            .Select(g => g.Key == TimelineCategory.Classification
                ? "Einstufung geändert"
                : TimelineCategoryDisplay.Counted(g.Key, g.Count()))
            .ToList();

        if (parts.Count == 0)
        {
            return string.Empty;
        }
        if (parts.Count > MaxParts)
        {
            return string.Join(", ", parts.Take(MaxParts)) + " und mehr";
        }
        return parts.Count == 1
            ? parts[0]
            : string.Join(", ", parts.Take(parts.Count - 1)) + " und " + parts[^1];
    }

    /// <summary>"heute um 14:20", "gestern um …", "am 16.09. um …", with the year only when it differs.</summary>
    public static string VisitLabel(DateTime visitLocal, DateTime todayLocal)
    {
        var time = visitLocal.ToString("HH:mm");
        var day = visitLocal.Date;
        if (day == todayLocal.Date)
        {
            return $"heute um {time}";
        }
        if (day == todayLocal.Date.AddDays(-1))
        {
            return $"gestern um {time}";
        }
        var date = day.Year == todayLocal.Year ? visitLocal.ToString("dd.MM.") : visitLocal.ToString("dd.MM.yyyy");
        return $"am {date} um {time}";
    }

    private static int Rank(TimelineCategory category)
    {
        var index = Array.IndexOf(Weight, category);
        return index < 0 ? int.MaxValue : index;
    }
}
