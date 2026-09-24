using NOOSE_Website.Models.Timeline;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>"New since your last visit": which view counts as the last visit, and which timeline entries are new.</summary>
public sealed class RecordVisitsTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 18, 0, 0, DateTimeKind.Utc);

    private static DateTime Ago(int minutes) => Now.AddMinutes(-minutes);

    private static TimelineEntry Entry(
        DateTime at, TimelineCategory category = TimelineCategory.Comment, string? actorId = "other",
        DateTime? recordedAt = null)
        => new(at, category, "Titel", null, "Name", null, null, actorId, recordedAt);

    // ---------- previous visit ----------

    [Fact]
    public void Without_any_view_there_is_no_previous_visit()
    {
        Assert.Null(RecordVisits.PreviousSessionEnd([], Now));
    }

    [Fact]
    public void A_view_two_hours_ago_is_the_previous_visit()
    {
        Assert.Equal(Ago(120), RecordVisits.PreviousSessionEnd([Ago(120)], Now));
    }

    [Fact]
    public void A_view_ten_minutes_ago_is_this_visit_and_there_was_none_before()
    {
        Assert.Null(RecordVisits.PreviousSessionEnd([Ago(10)], Now));
    }

    [Fact]
    public void A_chain_of_short_gaps_is_one_visit_and_the_view_before_it_is_the_previous_one()
    {
        // every step under half an hour, then a day's pause
        DateTime[] views = [Ago(5), Ago(25), Ago(50), Ago(70), Ago(70 + 24 * 60)];

        Assert.Equal(Ago(70 + 24 * 60), RecordVisits.PreviousSessionEnd(views, Now));
    }

    [Fact]
    public void The_double_row_of_prerendering_does_not_move_the_previous_visit()
    {
        // the page logs once while prerendering and once when interactive, a second apart
        DateTime[] views = [Now, Now.AddSeconds(-1), Ago(300)];

        Assert.Equal(Ago(300), RecordVisits.PreviousSessionEnd(views, Now));
    }

    [Fact]
    public void Exactly_the_session_gap_already_separates_two_visits()
    {
        var gap = (int)RecordVisits.SessionGap.TotalMinutes;

        Assert.Equal(Ago(gap), RecordVisits.PreviousSessionEnd([Ago(gap)], Now));
        Assert.Null(RecordVisits.PreviousSessionEnd([Now.AddMinutes(-gap).AddSeconds(1)], Now));
    }

    [Fact]
    public void A_view_stamped_ahead_of_now_does_not_break_the_chain()
    {
        // clock skew between web node and database: the fresh row is a few seconds "in the future"
        DateTime[] views = [Now.AddSeconds(5), Ago(10), Ago(200)];

        Assert.Equal(Ago(200), RecordVisits.PreviousSessionEnd(views, Now));
    }

    // ---------- is it new ----------

    [Fact]
    public void An_entry_by_someone_else_after_the_visit_is_new()
    {
        Assert.True(RecordVisits.IsNew(Entry(Ago(10)), Ago(60), "me"));
    }

    [Fact]
    public void The_viewers_own_entry_is_never_new()
    {
        Assert.False(RecordVisits.IsNew(Entry(Ago(10), actorId: "me"), Ago(60), "me"));
    }

    [Fact]
    public void An_entry_without_a_known_actor_is_new()
    {
        // a hidden tip submitter or a background job: nobody the viewer is
        Assert.True(RecordVisits.IsNew(Entry(Ago(10), actorId: null), Ago(60), "me"));
    }

    [Fact]
    public void An_entry_at_or_before_the_visit_is_not_new()
    {
        Assert.False(RecordVisits.IsNew(Entry(Ago(60)), Ago(60), "me"));
        Assert.False(RecordVisits.IsNew(Entry(Ago(90)), Ago(60), "me"));
    }

    [Fact]
    public void A_backdated_observation_entered_after_the_visit_is_new()
    {
        var observation = Entry(Ago(7 * 24 * 60), TimelineCategory.Observation, recordedAt: Ago(10));

        Assert.True(RecordVisits.IsNew(observation, Ago(60), "me"));
    }

    [Fact]
    public void An_observation_dated_after_the_visit_but_entered_before_it_is_not_new()
    {
        var observation = Entry(Ago(10), TimelineCategory.Observation, recordedAt: Ago(90));

        Assert.False(RecordVisits.IsNew(observation, Ago(60), "me"));
    }

    [Fact]
    public void Without_a_previous_visit_nothing_is_new()
    {
        Assert.False(RecordVisits.IsNew(Entry(Ago(1)), null, "me"));
    }

    // ---------- summary ----------

    [Fact]
    public void Nothing_new_reads_as_an_empty_line()
    {
        Assert.Equal(string.Empty, RecordVisits.Summarise([]));
    }

    [Fact]
    public void One_and_many_take_their_own_noun()
    {
        Assert.Equal("1 Kommentar", RecordVisits.Summarise([Entry(Ago(1))]));
        Assert.Equal("3 Kommentare", RecordVisits.Summarise([Entry(Ago(1)), Entry(Ago(2)), Entry(Ago(3))]));
    }

    [Fact]
    public void A_classification_is_named_without_a_count_and_leads_the_line()
    {
        TimelineEntry[] fresh =
        [
            Entry(Ago(1), TimelineCategory.Comment),
            Entry(Ago(2), TimelineCategory.Comment),
            Entry(Ago(3), TimelineCategory.Comment),
            Entry(Ago(4), TimelineCategory.Doc),
            Entry(Ago(5), TimelineCategory.Classification),
            Entry(Ago(6), TimelineCategory.Classification),
        ];

        Assert.Equal("Einstufung geändert, 1 Dok und 3 Kommentare", RecordVisits.Summarise(fresh));
    }

    [Fact]
    public void A_long_line_names_the_heaviest_parts_and_folds_the_rest()
    {
        TimelineCategory[] categories =
        [
            TimelineCategory.Change, TimelineCategory.Photo, TimelineCategory.Source, TimelineCategory.Link,
            TimelineCategory.Comment, TimelineCategory.Doc, TimelineCategory.Followup, TimelineCategory.Relation,
        ];
        var fresh = categories.Select((c, i) => Entry(Ago(i + 1), c)).ToList();

        Assert.Equal(
            "1 Dok, 1 Kommentar, 1 Beziehung, 1 Verknüpfung, 1 Quelle, 1 Foto und mehr",
            RecordVisits.Summarise(fresh));
    }

    [Fact]
    public void Every_category_has_a_plural_of_its_own()
    {
        foreach (var category in Enum.GetValues<TimelineCategory>())
        {
            var one = TimelineCategoryDisplay.Counted(category, 1);
            var many = TimelineCategoryDisplay.Counted(category, 2);
            Assert.StartsWith("1 ", one);
            Assert.StartsWith("2 ", many);
            Assert.NotEqual(one[2..], many[2..]);
        }
    }

    // ---------- visit label ----------

    [Fact]
    public void The_visit_reads_as_today_yesterday_or_a_date()
    {
        var today = new DateTime(2026, 9, 23, 18, 0, 0);

        Assert.Equal("heute um 09:05", RecordVisits.VisitLabel(new DateTime(2026, 9, 23, 9, 5, 0), today));
        Assert.Equal("gestern um 23:40", RecordVisits.VisitLabel(new DateTime(2026, 9, 22, 23, 40, 0), today));
        Assert.Equal("am 16.09. um 14:20", RecordVisits.VisitLabel(new DateTime(2026, 9, 16, 14, 20, 0), today));
        Assert.Equal("am 30.12.2025 um 08:00", RecordVisits.VisitLabel(new DateTime(2025, 12, 30, 8, 0, 0), today));
    }
}
