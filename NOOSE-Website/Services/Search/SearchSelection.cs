using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search;

/// <summary>The three batch actions the search page offers on picked hits.</summary>
public enum SearchBatchAction
{
    Link,
    Tag,
    Follow,
}

/// <summary>Which search hits the selection mode may pick, and what a picked set can do.</summary>
/// <remarks>Only the hit side lives here. Which record type an action may touch is <see cref="RecordBatch"/>'s,
/// because the services enforce it; this class only reads it.</remarks>
public static class SearchSelection
{
    private static readonly SearchBatchAction[] Actions = Enum.GetValues<SearchBatchAction>();

    /// <summary>Stable key of a picked hit.</summary>
    public static string Key(SearchHit hit) => $"{hit.Category}:{hit.TargetId}";

    /// <summary>The record a picked hit stands for.</summary>
    public static (string Type, string Id) Ref(SearchHit hit) => (hit.Category, hit.TargetId);

    /// <summary>Record types an action may touch.</summary>
    public static IReadOnlySet<string> Types(SearchBatchAction action) => action switch
    {
        SearchBatchAction.Link => RecordBatch.Linkable,
        SearchBatchAction.Tag => RecordBatch.Taggable,
        _ => RecordBatch.Followable,
    };

    /// <summary>A record hit of a type at least one action can handle.</summary>
    /// <remarks>A content hit names its parent and turns up once per comment or source, so ticking it would act on
    /// a record the row does not show. Log, admin and personal rows are no records at all.</remarks>
    public static bool IsSelectable(SearchHit hit)
        => string.IsNullOrEmpty(hit.TargetType)
            && SearchCatalog.Find(hit.Category) is { Shape: SearchHitShape.Record }
            && Actions.Any(a => Types(a).Contains(hit.Category));

    public static bool Supports(SearchHit hit, SearchBatchAction action)
        => IsSelectable(hit) && Types(action).Contains(hit.Category);

    /// <summary>Picked records an action can handle.</summary>
    public static IReadOnlyList<(string Type, string Id)> Refs(IEnumerable<SearchHit> picked, SearchBatchAction action)
        => picked.Where(h => Supports(h, action)).Select(Ref).Distinct().ToList();

    /// <summary>One German line for the snackbar; parts that are zero are left out.</summary>
    /// <param name="skipped">Picked records the action could not take at all, e.g. a case for a tag.</param>
    public static string Summary(SearchBatchAction action, BatchOutcome outcome, int skipped)
    {
        var parts = new List<string>();
        if (outcome.Done > 0)
        {
            parts.Add(outcome.Done + action switch
            {
                SearchBatchAction.Link => " verknüpft",
                SearchBatchAction.Tag => " verschlagwortet",
                _ => " beobachtet",
            });
        }
        if (outcome.Unchanged > 0)
        {
            var one = outcome.Unchanged == 1;
            parts.Add(outcome.Unchanged + action switch
            {
                SearchBatchAction.Link => one ? " war schon verknüpft" : " waren schon verknüpft",
                SearchBatchAction.Tag => one ? " hatte die Stichworte schon" : " hatten die Stichworte schon",
                _ => " beobachtest du schon",
            });
        }
        if (outcome.Refused > 0)
        {
            parts.Add($"{outcome.Refused} nicht erlaubt");
        }
        if (skipped > 0)
        {
            parts.Add(skipped == 1 ? "1 passt nicht dazu" : $"{skipped} passen nicht dazu");
        }
        return parts.Count == 0 ? "Nichts geändert." : string.Join(" · ", parts);
    }
}
