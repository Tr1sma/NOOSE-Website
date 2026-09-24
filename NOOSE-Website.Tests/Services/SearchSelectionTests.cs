using NOOSE_Website.Models.Common;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services;

/// <summary>Which search hits the selection mode may pick, and the line it reports afterwards.</summary>
public sealed class SearchSelectionTests
{
    private static SearchHit Hit(string category, string id = "x1", string? targetType = null)
        => new(category, id, "Titel", string.Empty, string.Empty, targetType);

    [Fact]
    public void A_record_hit_is_selectable()
    {
        Assert.True(SearchSelection.IsSelectable(Hit("Person")));
        Assert.True(SearchSelection.IsSelectable(Hit("Case")));
    }

    [Fact]
    public void Content_log_and_personal_hits_are_not()
    {
        // a comment names its parent person; ticking it would act on a record the row does not show
        Assert.False(SearchSelection.IsSelectable(Hit("Comment", "p1", "Person")));
        Assert.False(SearchSelection.IsSelectable(Hit("PersonDoc", "p1", "Person")));
        Assert.False(SearchSelection.IsSelectable(Hit("AuditLog", "17")));
        Assert.False(SearchSelection.IsSelectable(Hit("SavedSearch")));
        Assert.False(SearchSelection.IsSelectable(Hit("HandbookArticle", "suchen")));
        Assert.False(SearchSelection.IsSelectable(Hit("NoSuchCategory")));
    }

    [Fact]
    public void A_record_no_action_can_take_is_not_selectable()
    {
        Assert.False(SearchSelection.IsSelectable(Hit("KassenBuchung")));
        Assert.False(SearchSelection.IsSelectable(Hit("EvidenceItem")));
    }

    [Theory]
    [InlineData("Person", true, true, true)]
    [InlineData("Case", true, true, true)]
    [InlineData("Law", true, false, false)]
    [InlineData("Job", true, true, false)]
    [InlineData("Document", true, false, false)]
    [InlineData("Agent", true, false, true)]
    [InlineData("AgentActivity", false, true, false)]
    public void Each_type_offers_its_actions(string category, bool link, bool tag, bool follow)
    {
        var hit = Hit(category);

        Assert.Equal(link, SearchSelection.Supports(hit, SearchBatchAction.Link));
        Assert.Equal(tag, SearchSelection.Supports(hit, SearchBatchAction.Tag));
        Assert.Equal(follow, SearchSelection.Supports(hit, SearchBatchAction.Follow));
    }

    [Fact]
    public void Refs_keep_only_what_the_action_takes_once_each()
    {
        var picked = new[] { Hit("Person", "p1"), Hit("Law", "l1"), Hit("Person", "p1"), Hit("Case", "c1") };

        Assert.Equal([("Person", "p1"), ("Case", "c1")], SearchSelection.Refs(picked, SearchBatchAction.Tag));
        Assert.Equal([("Person", "p1"), ("Law", "l1"), ("Case", "c1")], SearchSelection.Refs(picked, SearchBatchAction.Link));
    }

    [Fact]
    public void The_key_tells_types_apart()
    {
        Assert.NotEqual(SearchSelection.Key(Hit("Person", "1")), SearchSelection.Key(Hit("Case", "1")));
        Assert.Equal(SearchSelection.Key(Hit("Person", "1")), SearchSelection.Key(Hit("Person", "1")));
    }

    [Fact]
    public void The_summary_names_every_part_that_happened()
    {
        Assert.Equal("5 verknüpft · 2 waren schon verknüpft · 1 nicht erlaubt · 3 passen nicht dazu",
            SearchSelection.Summary(SearchBatchAction.Link, new BatchOutcome(5, 2, 1), 3));
        Assert.Equal("1 verschlagwortet · 1 hatte die Stichworte schon · 1 passt nicht dazu",
            SearchSelection.Summary(SearchBatchAction.Tag, new BatchOutcome(1, 1, 0), 1));
        Assert.Equal("4 beobachtest du schon",
            SearchSelection.Summary(SearchBatchAction.Follow, new BatchOutcome(0, 4, 0), 0));
    }

    [Fact]
    public void Nothing_done_says_so()
    {
        Assert.Equal("Nichts geändert.", SearchSelection.Summary(SearchBatchAction.Link, new BatchOutcome(0, 0, 0), 0));
    }
}
