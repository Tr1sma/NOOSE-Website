using Microsoft.AspNetCore.Components;
using NOOSE_Website.Components.Common.Shared;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Navigation;

/// <summary>The filter half of a list address: how it is read back, and how a saved view builds it.</summary>
/// <remarks>
/// A saved view is built from the page's fields, not read off the address bar - the address bar lags behind the last
/// filter change. That only works if what <see cref="QueryState.BuildRoute"/> writes reads back unchanged.
/// </remarks>
public class QueryStateTests
{
    private sealed class FakeNav : NavigationManager
    {
        public FakeNav(string uri) => Initialize("https://noose.test/", uri);

        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    // ==================== building a route ====================

    [Fact]
    public void A_route_without_any_set_filter_is_the_bare_list()
        // the list's default view; a "?" with nothing behind it would be a different string for the same page
        => Assert.Equal("/personen", QueryState.BuildRoute("/personen",
            ("q", null), ("einstufung", string.Empty), ("archiv", "   ")));

    [Fact]
    public void A_route_keeps_the_order_given_and_drops_the_empty_pairs()
        => Assert.Equal("/personen?q=Rot&aktualitaet=Red", QueryState.BuildRoute("/personen",
            ("q", "Rot"), ("einstufung", null), ("aktualitaet", "Red")));

    [Fact]
    public void A_route_encodes_what_would_otherwise_split_the_query()
    {
        var route = QueryState.BuildRoute("/personen", ("q", "A&B=C #1"));

        Assert.Equal("/personen?q=A%26B%3DC%20%231", route);
    }

    [Theory]
    [InlineData("Müller")]
    [InlineData("A&B=C")]
    [InlineData("a+b")]
    [InlineData("zwei Wörter")]
    [InlineData("50% #1 / 2")]
    public void A_built_route_reads_back_to_the_same_value(string value)
    {
        var route = QueryState.BuildRoute("/personen", ("q", value), ("einstufung", "SuspicionCase"));
        var query = route[route.IndexOf('?')..];

        Assert.Equal(value, QueryState.Read(query, "q"));
        Assert.Equal(Classification.SuspicionCase, QueryState.ReadEnum<Classification>(query, "einstufung"));
    }

    [Fact]
    public void AnySet_is_false_only_when_every_pair_is_empty()
    {
        Assert.False(QueryState.AnySet(("q", null), ("einstufung", " ")));
        Assert.False(QueryState.AnySet());
        Assert.True(QueryState.AnySet(("q", null), ("einstufung", "SuspicionCase")));
    }

    // ==================== reading ====================

    [Fact]
    public void Reading_takes_a_query_with_or_without_its_question_mark()
    {
        Assert.Equal("Rot", QueryState.Read("?q=Rot", "q"));
        Assert.Equal("Rot", QueryState.Read("q=Rot", "q"));
        Assert.Null(QueryState.Read(string.Empty, "q"));
        Assert.Null(QueryState.Read((string?)null, "q"));
    }

    [Fact]
    public void Reading_the_string_and_the_navigation_manager_agree()
    {
        var nav = new FakeNav("https://noose.test/personen?q=M%C3%BCller&einstufung=SuspicionCase&meine=1");
        const string query = "?q=M%C3%BCller&einstufung=SuspicionCase&meine=1";

        Assert.Equal(QueryState.Read(query, "q"), QueryState.Read(nav, "q"));
        Assert.Equal(QueryState.ReadEnum<Classification>(query, "einstufung"),
            QueryState.ReadEnum<Classification>(nav, "einstufung"));
        Assert.Equal(QueryState.ReadFlag(query, "meine"), QueryState.ReadFlag(nav, "meine"));
        Assert.Equal("Müller", QueryState.Read(nav, "q"));
    }

    [Theory]
    [InlineData("?meine=1", true)]
    [InlineData("?meine=true", true)]
    [InlineData("?meine=True", true)]
    [InlineData("?meine=0", false)]
    [InlineData("?meine=false", false)]
    [InlineData("?meine=ja", false)]
    [InlineData("", false)]
    public void A_flag_reads_one_and_true_as_set(string query, bool expected)
        => Assert.Equal(expected, QueryState.ReadFlag(query, "meine"));

    [Fact]
    public void The_request_log_anomaly_box_survives_a_reload()
        // it writes "1" and used to read it back with bool.TryParse, which refuses "1": the box always came back empty
        => Assert.True(QueryState.ReadFlag("?auf=1", "auf"));

    [Theory]
    [InlineData("?einstufung=99")]
    [InlineData("?einstufung=-1")]
    [InlineData("?einstufung=Unsinn")]
    public void An_enum_value_no_member_carries_reads_as_absent(string query)
        // TryParse alone accepts any number, and the list then filtered on a value no switch knows
        => Assert.Null(QueryState.ReadEnum<Classification>(query, "einstufung"));

    [Fact]
    public void An_enum_member_reads_regardless_of_case()
        => Assert.Equal(Classification.SuspicionCase, QueryState.ReadEnum<Classification>("?einstufung=suspicioncase", "einstufung"));
}
