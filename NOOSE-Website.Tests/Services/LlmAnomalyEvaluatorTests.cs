using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The four NOOSEI misuse rules; every guard here exists because a naive threshold flags the wrong thing.</summary>
public class LlmAnomalyEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Unspecified);

    private static LlmAnomalyThresholds Thresholds(Action<LlmAnomalyThresholds>? configure = null)
    {
        var t = new LlmAnomalyThresholds();
        configure?.Invoke(t);
        return t;
    }

    private static LlmQuotaStatus Status(string agentId = "a1", long available = 50_000, long consumed = 0,
        Rank rank = Rank.SpecialAgent)
        => new(agentId, "Falke-" + agentId, rank, 2026, 32, available, 0, consumed, 0, false);

    private static LlmUsageRow Row(int minutesAgo, long tokens, string prompt = "Frage", string agentId = "a1")
        => new(agentId, "Falke-" + agentId, Now.AddMinutes(-minutesAgo), tokens, Fingerprint(prompt), prompt);

    private static string Fingerprint(string prompt) => prompt.ToLowerInvariant().Trim();

    // ---- R1 cost spike ----

    [Fact]
    public void CostSpike_FlagsARequestAboveTheFactor()
        => Assert.True(LlmAnomalyEvaluator.IsCostSpike(15_000, baselineMean: 2_000, baselineCount: 30, Thresholds()));

    [Fact]
    public void CostSpike_NeedsEnoughHistory()
    {
        // without the baseline guard an agent's second request is always a "spike"
        Assert.False(LlmAnomalyEvaluator.IsCostSpike(15_000, 2_000, baselineCount: 3, Thresholds()));
    }

    [Fact]
    public void CostSpike_NeedsToClearTheAbsoluteFloor()
    {
        // a week of 20-token spell-checks must not make a normal 150-token answer an outlier
        Assert.False(LlmAnomalyEvaluator.IsCostSpike(150, baselineMean: 20, baselineCount: 40, Thresholds()));
        Assert.True(LlmAnomalyEvaluator.IsCostSpike(150, 20, 40, Thresholds(t => t.SpikeMinTokens = 100)));
    }

    [Fact]
    public void CostSpike_IsSilentWhenDisabled()
        => Assert.False(LlmAnomalyEvaluator.IsCostSpike(99_999, 100, 100, Thresholds(t => t.SpikeEnabled = false)));

    // ---- R2 burn rate ----

    [Fact]
    public void BurnRate_FlagsAShareBurnedInsideTheWindow()
    {
        var rows = new[] { Row(300, 20_000), Row(290, 12_000) };

        var flags = LlmAnomalyEvaluator.Evaluate(rows, [Status(consumed: 32_000)], new Dictionary<string, double>(),
            new Dictionary<Rank, double>(), Thresholds(), Now);

        var flag = Assert.Single(flags, f => f.Kind == LlmAnomalyKind.BurnRate);
        Assert.Contains("64 %", flag.Detail.Replace(' ', ' '));
    }

    [Fact]
    public void BurnRate_IgnoresTheSameVolumeSpreadWider()
    {
        // same 32.000 tokens, but four days apart instead of ten minutes
        var rows = new[] { Row(5_000, 20_000), Row(200, 12_000) };

        var flags = LlmAnomalyEvaluator.Evaluate(rows, [Status(consumed: 32_000)], new Dictionary<string, double>(),
            new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.DoesNotContain(flags, f => f.Kind == LlmAnomalyKind.BurnRate);
    }

    [Fact]
    public void BurnRate_UsesTheAgentsOwnAvailable_NotTheRankDefault()
    {
        var rows = new[] { Row(60, 32_000) };

        var big = LlmAnomalyEvaluator.Evaluate(rows, [Status(available: 200_000, consumed: 32_000)],
            new Dictionary<string, double>(), new Dictionary<Rank, double>(), Thresholds(), Now);
        var small = LlmAnomalyEvaluator.Evaluate(rows, [Status(available: 40_000, consumed: 32_000)],
            new Dictionary<string, double>(), new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.DoesNotContain(big, f => f.Kind == LlmAnomalyKind.BurnRate);
        Assert.Contains(small, f => f.Kind == LlmAnomalyKind.BurnRate);
    }

    // ---- R3 burst ----

    [Fact]
    public void Burst_NeedsBothTheCountAndTheDuplicates()
    {
        // genuinely different questions: a fast worker is not a misuse case
        string[] questions =
        [
            "Wer führt die Ballas an?", "Welche Fahrzeuge sind auf Max zugelassen?",
            "Zeig mir den Verlauf der Operation Nachtfalke.", "Gibt es offene Wiedervorlagen?",
            "Was steht im Kurzbrief zur Partei?", "Welche Taskforces laufen aktuell?",
            "Wie viele Doks hat die Fraktion?", "Wann war die letzte Observation?",
            "Welche Personen wohnen in Vinewood?", "Was hat sich diese Woche geändert?",
        ];
        var distinct = Enumerable.Range(0, 10).Select(i => Row(i, 100, questions[i])).ToList();
        var repeated = Enumerable.Range(0, 10)
            .Select(i => Row(i, 100, i < 5 ? "Immer dasselbe" : questions[i])).ToList();

        var noFlag = LlmAnomalyEvaluator.Evaluate(distinct, [Status()], new Dictionary<string, double>(),
            new Dictionary<Rank, double>(), Thresholds(), Now);
        var flagged = LlmAnomalyEvaluator.Evaluate(repeated, [Status()], new Dictionary<string, double>(),
            new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.DoesNotContain(noFlag, f => f.Kind == LlmAnomalyKind.Burst);
        Assert.Contains(flagged, f => f.Kind == LlmAnomalyKind.Burst);
    }

    [Fact]
    public void Burst_TreatsNearIdenticalPromptsAsDuplicates()
    {
        var rows = new List<LlmUsageRow>
        {
            new("a1", "Falke", Now.AddMinutes(-5), 100, "fp1", "Wer führt die Ballas an?"),
            new("a1", "Falke", Now.AddMinutes(-4), 100, "fp2", "Wer führt die Ballas an!"),
            new("a1", "Falke", Now.AddMinutes(-3), 100, "fp3", "Wer führt die Ballas an."),
            new("a1", "Falke", Now.AddMinutes(-2), 100, "fp4", "Etwas ganz anderes"),
            new("a1", "Falke", Now.AddMinutes(-1), 100, "fp5", "Noch etwas anderes"),
            new("a1", "Falke", Now, 100, "fp6", "Und noch was"),
            new("a1", "Falke", Now.AddSeconds(-30), 100, "fp7", "Weiter"),
            new("a1", "Falke", Now.AddSeconds(-10), 100, "fp8", "Und weiter"),
        };

        var flags = LlmAnomalyEvaluator.Evaluate(rows, [Status()], new Dictionary<string, double>(),
            new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.Contains(flags, f => f.Kind == LlmAnomalyKind.Burst);
    }

    [Fact]
    public void Burst_IsSilentWhenDisabled()
    {
        var rows = Enumerable.Range(0, 10).Select(i => Row(i, 100, "Immer dasselbe")).ToList();

        var flags = LlmAnomalyEvaluator.Evaluate(rows, [Status()], new Dictionary<string, double>(),
            new Dictionary<Rank, double>(), Thresholds(t => t.BurstEnabled = false), Now);

        Assert.DoesNotContain(flags, f => f.Kind == LlmAnomalyKind.Burst);
    }

    // ---- R4 outlier ----

    [Fact]
    public void Outlier_FlagsAgainstTheOwnTrailingAverage()
    {
        var flags = LlmAnomalyEvaluator.Evaluate([Row(60, 30_000)], [Status(consumed: 30_000)],
            new Dictionary<string, double> { ["a1"] = 5_000 }, new Dictionary<Rank, double>(), Thresholds(), Now);

        var flag = Assert.Single(flags, f => f.Kind == LlmAnomalyKind.Outlier);
        Assert.Contains("sonst im Schnitt", flag.Detail);
    }

    [Fact]
    public void Outlier_IgnoresAnAgentStillNearTheirOwnAverage()
    {
        var flags = LlmAnomalyEvaluator.Evaluate([Row(60, 6_000)], [Status(consumed: 6_000)],
            new Dictionary<string, double> { ["a1"] = 5_000 }, new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.DoesNotContain(flags, f => f.Kind == LlmAnomalyKind.Outlier);
    }

    [Fact]
    public void Outlier_FlagsAgainstTheRankAverage_OnlyWithEnoughPeers()
    {
        var statuses = new[]
        {
            Status("a1", consumed: 30_000),
            Status("a2", consumed: 5_000),
            Status("a3", consumed: 5_000),
        };
        var rankMean = new Dictionary<Rank, double> { [Rank.SpecialAgent] = 5_000 };

        var withPeers = LlmAnomalyEvaluator.Evaluate([Row(60, 30_000)], statuses,
            new Dictionary<string, double>(), rankMean, Thresholds(), Now);
        var alone = LlmAnomalyEvaluator.Evaluate([Row(60, 30_000)], [statuses[0]],
            new Dictionary<string, double>(), rankMean, Thresholds(), Now);

        Assert.Contains(withPeers, f => f.Kind == LlmAnomalyKind.Outlier);
        Assert.DoesNotContain(alone, f => f.Kind == LlmAnomalyKind.Outlier);
    }

    [Fact]
    public void Outlier_IsSilentWhenDisabled()
    {
        var flags = LlmAnomalyEvaluator.Evaluate([Row(60, 30_000)], [Status(consumed: 30_000)],
            new Dictionary<string, double> { ["a1"] = 100 }, new Dictionary<Rank, double>(),
            Thresholds(t => t.OutlierEnabled = false), Now);

        Assert.DoesNotContain(flags, f => f.Kind == LlmAnomalyKind.Outlier);
    }

    // ---- ordering ----

    [Fact]
    public void Evaluate_ReturnsTheWorstFirst_AndAtMostOneFlagPerAgentAndRule()
    {
        var rows = Enumerable.Range(0, 12).Select(i => Row(i, 5_000, "Immer dasselbe")).ToList();

        var flags = LlmAnomalyEvaluator.Evaluate(rows, [Status(consumed: 60_000)],
            new Dictionary<string, double> { ["a1"] = 1_000 }, new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.Equal(flags.OrderByDescending(f => f.Grade).Select(f => f.Grade), flags.Select(f => f.Grade));
        Assert.Equal(flags.Count, flags.Select(f => (f.AgentId, f.Kind)).Distinct().Count());
    }

    [Fact]
    public void Evaluate_IgnoresAnAgentWithoutRequests()
    {
        var flags = LlmAnomalyEvaluator.Evaluate([], [Status(consumed: 99_000)],
            new Dictionary<string, double> { ["a1"] = 1 }, new Dictionary<Rank, double>(), Thresholds(), Now);

        Assert.Empty(flags);
    }
}
