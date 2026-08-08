using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The replay window. Two things must hold whatever is in the table: the wire shape is valid, and a turn
/// is never cut in half.</summary>
public class NooseiHistoryWindowTests
{
    private const int Wide = 1_000_000;

    private static NooseiHistoryRow User(string text) => new("user", text);

    private static NooseiHistoryRow Answer(string text) => new("assistant", text);

    private static NooseiHistoryRow Calls(string id, string tool)
        => new(NooseiHistoryWindow.ToolCallRole, null,
            ToolCallsJson: NooseiHistoryWindow.Serialize([new LlmToolCall(id, tool, "{}")]));

    private static NooseiHistoryRow Result(string id, string tool, string text) => new("tool", text, tool, id);

    private static List<NooseiHistoryRow> Turn(string question, string id = "c1", string result = "Treffer")
        => [User(question), Calls(id, "suche_akten"), Result(id, "suche_akten", result), Answer("Antwort " + question)];

    /// <summary>A tool-call round carries no text at all, so every content check has to tolerate null.</summary>
    private static bool Says(LlmMessage message, string needle)
        => message.Content is { } content && content.Contains(needle, StringComparison.Ordinal);

    [Fact]
    public void Build_ReplaysAToolExchangeAsRealToolRoles()
    {
        var window = NooseiHistoryWindow.Build(Turn("Erste"), sameScope: true, Wide, 8);

        Assert.Collection(window,
            m => Assert.Equal(LlmRole.User, m.Role),
            m =>
            {
                Assert.Equal(LlmRole.Assistant, m.Role);
                Assert.Equal("c1", Assert.Single(m.ToolCalls!).Id);
            },
            m =>
            {
                Assert.Equal(LlmRole.Tool, m.Role);
                Assert.Equal("c1", m.ToolCallId);
                Assert.Equal("suche_akten", m.Name);
            },
            m => Assert.Equal(LlmRole.Assistant, m.Role));
    }

    [Fact]
    public void Build_DegradesAToolRowWithoutItsCallRow_BecauseAnUnpairedToolRoleIsA400()
    {
        // every row stored before tool calls were kept looks like this, so it cannot be left to tidy data
        List<NooseiHistoryRow> rows = [User("Frage"), Result("c1", "lies_akte", "Akteninhalt"), Answer("Antwort")];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        Assert.DoesNotContain(window, m => m.Role == LlmRole.Tool);
        var context = Assert.Single(window, m => Says(m, NooseiHistoryWindow.FlattenedToolPrefix));
        Assert.Contains("Akteninhalt", context.Content);
    }

    [Fact]
    public void Build_DropsACallRowWhoseResultIsMissing()
    {
        // an assistant message with tool_calls and no answers is the same 400 from the other side
        List<NooseiHistoryRow> rows = [User("Frage"), Calls("c1", "lies_akte"), Answer("Antwort")];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        Assert.DoesNotContain(window, m => m.ToolCalls is { Count: > 0 });
        Assert.Equal(2, window.Count);
    }

    [Fact]
    public void Build_KeepsOnlyTheAnsweredCallsOfARound()
    {
        var partly = new NooseiHistoryRow(NooseiHistoryWindow.ToolCallRole, null,
            ToolCallsJson: NooseiHistoryWindow.Serialize(
                [new LlmToolCall("c1", "suche_akten", "{}"), new LlmToolCall("c2", "lies_akte", "{}")]));
        List<NooseiHistoryRow> rows = [User("Frage"), partly, Result("c1", "suche_akten", "Treffer"), Answer("Antwort")];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        var call = Assert.Single(window, m => m.ToolCalls is { Count: > 0 });
        Assert.Equal("c1", Assert.Single(call.ToolCalls!).Id);
    }

    [Fact]
    public void Build_DropsEveryToolRow_WhenTheScopeChanged()
    {
        var window = NooseiHistoryWindow.Build(Turn("Frage", result: "Geheimes"), sameScope: false, Wide, 8);

        Assert.DoesNotContain(window, m => m.Role == LlmRole.Tool || m.ToolCalls is { Count: > 0 });
        Assert.DoesNotContain(window, m => Says(m, "Geheimes"));
        // the exchange itself survives; only the evidence is withheld
        Assert.Contains(window, m => m.Content == "Frage");
    }

    [Fact]
    public void Build_StartsAtAQuestion_EvenWhenTheRowCapCutIntoATurn()
    {
        List<NooseiHistoryRow> rows =
        [
            Result("c0", "lies_akte", "Rest eines älteren Turns"),
            Answer("Halbe Antwort"),
            .. Turn("Ganze Frage"),
        ];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        Assert.Equal(LlmRole.User, window[0].Role);
        Assert.DoesNotContain(window, m => Says(m, "Halbe Antwort"));
    }

    [Fact]
    public void Build_CutsOnlyOnATurnBoundary_WhenTheBudgetRunsOut()
    {
        List<NooseiHistoryRow> rows = [.. Turn("Alte", "c1", new string('x', 4_000)), .. Turn("Neue", "c2", "kurz")];

        // the newest turn fits, the compacted older one no longer does
        var window = NooseiHistoryWindow.Build(rows, sameScope: true, tokenBudget: 100, maxTurns: 8);

        Assert.Equal(LlmRole.User, window[0].Role);
        Assert.Equal("Neue", window[0].Content);
        Assert.DoesNotContain(window, m => m.Content == "Alte");
    }

    [Fact]
    public void Build_CompactsTheToolOutputOfEveryTurnButTheNewest()
    {
        var long1 = new string('a', NooseiHistoryWindow.CompactToolChars + 500);
        var long2 = new string('b', NooseiHistoryWindow.CompactToolChars + 500);
        List<NooseiHistoryRow> rows = [.. Turn("Alte", "c1", long1), .. Turn("Neue", "c2", long2)];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        var older = Assert.Single(window, m => Says(m, "aaa"));
        var newest = Assert.Single(window, m => Says(m, "bbb"));
        Assert.True(older.Content!.Length < long1.Length);
        Assert.Contains("gekürzt", older.Content);
        Assert.Equal(long2, newest.Content);
    }

    [Fact]
    public void Build_KeepsTheExchangeAndDropsTheEvidence_WhenEvenTheNewestTurnIsTooBig()
    {
        var rows = Turn("Frage", "c1", new string('x', 40_000));

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, tokenBudget: 200, maxTurns: 8);

        Assert.Equal(2, window.Count);
        Assert.Equal("Frage", window[0].Content);
        Assert.DoesNotContain(window, m => m.Role == LlmRole.Tool || m.ToolCalls is { Count: > 0 });
    }

    [Fact]
    public void Build_HonoursTheTurnCeiling_EvenWithBudgetToSpare()
    {
        List<NooseiHistoryRow> rows = [.. Turn("Eins", "c1"), .. Turn("Zwei", "c2"), .. Turn("Drei", "c3")];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, maxTurns: 2);

        Assert.DoesNotContain(window, m => m.Content == "Eins");
        Assert.Contains(window, m => m.Content == "Zwei");
        Assert.Contains(window, m => m.Content == "Drei");
    }

    [Fact]
    public void Build_SkipsErrorRows()
    {
        List<NooseiHistoryRow> rows = [User("Frage"), new("assistant", "Kaputt", IsError: true), User("Zweite"), Answer("Gut")];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        Assert.DoesNotContain(window, m => m.Content == "Kaputt");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Build_IsEmpty_WithoutABudget(int budget)
        => Assert.Empty(NooseiHistoryWindow.Build(Turn("Frage"), sameScope: true, budget, 8));

    [Fact]
    public void Build_IsEmpty_WithoutAQuestionToStartFrom()
        => Assert.Empty(NooseiHistoryWindow.Build([Answer("Antwort ohne Frage")], sameScope: true, Wide, 8));

    [Fact]
    public void Build_SurvivesUnusableToolCallJson()
    {
        List<NooseiHistoryRow> rows =
        [
            User("Frage"),
            new(NooseiHistoryWindow.ToolCallRole, null, ToolCallsJson: "{kein json"),
            Result("c1", "suche_akten", "Treffer"),
            Answer("Antwort"),
        ];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        // the call row is unreadable, so its result travels as context instead of an unpaired tool role
        Assert.DoesNotContain(window, m => m.Role == LlmRole.Tool);
        Assert.Contains(window, m => Says(m, NooseiHistoryWindow.FlattenedToolPrefix));
    }

    [Fact]
    public void Estimate_IsMonotoneAndNeverFree()
    {
        Assert.True(NooseiHistoryWindow.Estimate(null) > 0);
        Assert.True(NooseiHistoryWindow.Estimate(new string('x', 400)) > NooseiHistoryWindow.Estimate("kurz"));
    }

    [Fact]
    public void Serialize_RoundTripsThroughTheWindow()
    {
        var json = NooseiHistoryWindow.Serialize([new LlmToolCall("c9", "finde_akten", """{"typ":"Person"}""")]);
        List<NooseiHistoryRow> rows =
        [
            User("Frage"),
            new(NooseiHistoryWindow.ToolCallRole, null, ToolCallsJson: json),
            Result("c9", "finde_akten", "12 Akten"),
            Answer("Antwort"),
        ];

        var window = NooseiHistoryWindow.Build(rows, sameScope: true, Wide, 8);

        var call = Assert.Single(Assert.Single(window, m => m.ToolCalls is { Count: > 0 }).ToolCalls!);
        Assert.Equal("finde_akten", call.Name);
        Assert.Contains("Person", call.ArgumentsJson);
    }

    [Fact]
    public void Serialize_OfNothing_IsNull()
    {
        Assert.Null(NooseiHistoryWindow.Serialize(null));
        Assert.Null(NooseiHistoryWindow.Serialize([]));
    }
}
