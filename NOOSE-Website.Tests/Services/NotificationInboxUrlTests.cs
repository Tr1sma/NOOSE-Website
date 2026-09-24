using NOOSE_Website.Components.Common.Shared;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Notifications;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The inbox filter in the address: what survives a reload, a shared link and a saved view.</summary>
public sealed class NotificationInboxUrlTests
{
    [Fact]
    public void Unknown_and_numeric_types_drop_out()
    {
        // "1" would be Mention by number; only names count
        var filter = NotificationInboxUrl.Parse("?arten=1,Quatsch,99,jobassigned");

        Assert.Equal([NotificationType.JobAssigned], filter.Types);
    }

    [Theory]
    [InlineData("?ungelesen=1", true)]
    [InlineData("?ungelesen=true", true)]
    [InlineData("?ungelesen=0", false)]
    [InlineData("", false)]
    public void The_unread_flag_reads_both_spellings(string query, bool expected)
        => Assert.Equal(expected, NotificationInboxUrl.Parse(query).OnlyUnread);

    [Theory]
    [InlineData("?von=2026-13-01")]
    [InlineData("?von=01.05.2026")]
    [InlineData("?von=gestern")]
    public void A_broken_day_is_no_day(string query)
        => Assert.Null(NotificationInboxUrl.Parse(query).From);

    [Fact]
    public void A_day_reads_as_a_calendar_day()
    {
        var filter = NotificationInboxUrl.Parse("?von=2026-05-01&bis=2026-05-31");

        Assert.Equal(new DateTime(2026, 5, 1), filter.From);
        Assert.Equal(new DateTime(2026, 5, 31), filter.To);
    }

    [Fact]
    public void An_empty_filter_writes_nothing_to_the_address()
    {
        var pairs = NotificationInboxUrl.ToPairs(NotificationInboxFilter.None);

        Assert.False(QueryState.AnySet(pairs));
        Assert.Equal("/benachrichtigungen", QueryState.BuildRoute("/benachrichtigungen", pairs));
    }

    [Fact]
    public void Reading_what_was_written_gives_the_same_filter()
    {
        var filter = new NotificationInboxFilter([NotificationType.PublicTipReceived, NotificationType.Mention],
            new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), true);

        var route = QueryState.BuildRoute("/benachrichtigungen", NotificationInboxUrl.ToPairs(filter));
        var back = NotificationInboxUrl.Parse(new Uri("https://x" + route).Query);

        Assert.Equal("/benachrichtigungen?arten=Mention%2CPublicTipReceived&von=2026-05-01&bis=2026-05-31&ungelesen=1", route);
        Assert.Equal([NotificationType.Mention, NotificationType.PublicTipReceived], back.Types);
        Assert.Equal(filter.From, back.From);
        Assert.Equal(filter.To, back.To);
        Assert.True(back.OnlyUnread);
    }

    [Fact]
    public void The_page_number_is_not_part_of_the_filter()
    {
        // a saved view keeps these pairs, and "page 3" would open somewhere else tomorrow
        var pairs = NotificationInboxUrl.ToPairs(NotificationInboxUrl.Parse("?arten=Mention&seite=3"));

        Assert.DoesNotContain(pairs, p => p.Name == "seite");
    }

    [Fact]
    public void The_last_day_counts_whole()
    {
        var day = new DateTime(2026, 5, 10);

        var (fromUtc, toUtc) = NotificationInboxUrl.ToUtcRange(day, day);

        Assert.Equal(DateTimeKind.Utc, fromUtc!.Value.Kind);
        Assert.Equal(day, fromUtc.Value.ToLocalTime());
        Assert.Equal(day.AddDays(1), toUtc!.Value.ToLocalTime());
    }

    [Fact]
    public void Days_picked_the_wrong_way_round_still_give_the_window_between_them()
    {
        var early = new DateTime(2026, 5, 10);
        var late = new DateTime(2026, 5, 20);

        Assert.Equal(NotificationInboxUrl.ToUtcRange(early, late), NotificationInboxUrl.ToUtcRange(late, early));
    }

    [Fact]
    public void The_ends_of_the_calendar_in_a_typed_address_do_not_throw()
    {
        var filter = NotificationInboxUrl.Parse("?von=0001-01-01&bis=9999-12-31");

        var (fromUtc, toUtc) = NotificationInboxUrl.ToUtcRange(filter.From, filter.To);

        Assert.NotNull(fromUtc);
        Assert.Null(toUtc);
    }

    [Fact]
    public void An_open_side_stays_open()
    {
        var (fromUtc, toUtc) = NotificationInboxUrl.ToUtcRange(null, null);

        Assert.Null(fromUtc);
        Assert.Null(toUtc);
    }
}
