using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The wording three pickers share. It says the end day and never the reason.</summary>
public class AbsenceHintTests
{
    private static readonly DateOnly Reference = new(2026, 9, 21);

    private static Dictionary<string, DateOnly> Away(params (string Id, DateOnly Until)[] rows)
        => rows.ToDictionary(r => r.Id, r => r.Until, StringComparer.Ordinal);

    // ==================== one agent ====================

    [Fact]
    public void An_agent_who_is_there_gets_no_hint()
    {
        Assert.Equal(string.Empty, AbsenceHint.For(Away(), "a1", Reference));
        Assert.Equal(string.Empty, AbsenceHint.For(null, "a1", Reference));
    }

    [Fact]
    public void An_absent_agent_is_named_with_the_end_day()
        => Assert.Equal("abgemeldet bis 14.10.", AbsenceHint.For(Away(("a1", new(2026, 10, 14))), "a1", Reference));

    [Fact]
    public void The_year_appears_only_when_it_differs_from_the_day_being_asked_about()
    {
        Assert.Equal("abgemeldet bis 31.12.", AbsenceHint.For(Away(("a1", new(2026, 12, 31))), "a1", Reference));
        Assert.Equal("abgemeldet bis 04.01.2027", AbsenceHint.For(Away(("a1", new(2027, 1, 4))), "a1", Reference));
    }

    // ==================== what was actually picked ====================

    [Fact]
    public void Nothing_is_said_when_none_of_the_picked_agents_is_away()
    {
        var picked = new[] { ("a1", "Falke"), ("a2", "Uhu") };
        Assert.Equal(string.Empty, AbsenceHint.Selected(Away(("a3", new(2026, 10, 1))), picked, Reference));
        Assert.Equal(string.Empty, AbsenceHint.Selected(Away(), picked, Reference));
        Assert.Equal(string.Empty, AbsenceHint.Selected(null, picked, Reference));
    }

    [Fact]
    public void One_absent_pick_is_named_with_its_end_day()
        => Assert.Equal("Abgemeldet bis: Falke (14.10.)",
            AbsenceHint.Selected(Away(("a1", new(2026, 10, 14))), [("a1", "Falke")], Reference));

    [Fact]
    public void Several_absent_picks_are_listed_together()
        => Assert.Equal("Abgemeldet bis: Falke (14.10.), Uhu (02.11.)",
            AbsenceHint.Selected(
                Away(("a1", new(2026, 10, 14)), ("a2", new(2026, 11, 2))),
                [("a1", "Falke"), ("a2", "Uhu"), ("a3", "Reiher")],
                Reference));
}
