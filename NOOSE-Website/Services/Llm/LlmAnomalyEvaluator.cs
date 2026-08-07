using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>One request as the anomaly rules see it.</summary>
public readonly record struct LlmUsageRow(
    string AgentId,
    string? AgentName,
    DateTime LocalTimestamp,
    long QuotaTokens,
    string? PromptFingerprint,
    string? Prompt);

/// <summary>One flagged agent, ready for the admin overview.</summary>
public sealed record LlmAnomalyFlag(
    string AgentId,
    string? AgentName,
    LlmAnomalyKind Kind,
    string Detail,
    long Value,
    CounterIntelSeverity Grade);

/// <summary>The four NOOSEI misuse rules. Pure: everything arrives as an argument, so every combination is testable.</summary>
public static class LlmAnomalyEvaluator
{
    /// <summary>Pairs compared for near-identical prompts inside one window; a hard cap on the O(n²) part.</summary>
    private const int MaxBurstRows = 50;

    private const int MaxPromptCompareChars = 512;

    /// <summary>R1: one request costing a multiple of the rolling average. Judged at charge time, per row.</summary>
    public static bool IsCostSpike(long tokens, double baselineMean, int baselineCount, LlmAnomalyThresholds t)
        => t.SpikeEnabled
            && baselineCount >= t.SpikeMinBaselineCount
            && tokens >= t.SpikeMinTokens
            && baselineMean > 0
            && tokens >= baselineMean * t.SpikeFactor;

    /// <summary>R2-R4 over the running week; one flag per agent and rule, worst first.</summary>
    public static List<LlmAnomalyFlag> Evaluate(
        IReadOnlyList<LlmUsageRow> rows,
        IReadOnlyList<LlmQuotaStatus> statuses,
        IReadOnlyDictionary<string, double> ownTrailingMean,
        LlmAnomalyThresholds thresholds,
        DateTime nowLocal)
    {
        var flags = new List<LlmAnomalyFlag>();
        var byAgent = rows.GroupBy(r => r.AgentId).ToDictionary(g => g.Key, g => g.OrderBy(r => r.LocalTimestamp).ToList());

        foreach (var status in statuses)
        {
            if (!byAgent.TryGetValue(status.AgentId, out var agentRows) || agentRows.Count == 0)
            {
                continue;
            }

            if (BurnRate(agentRows, status, thresholds) is { } burn)
            {
                flags.Add(burn);
            }
            if (Burst(agentRows, status, thresholds) is { } burst)
            {
                flags.Add(burst);
            }
            if (Outlier(status, ownTrailingMean, statuses, thresholds) is { } outlier)
            {
                flags.Add(outlier);
            }
        }

        return flags
            .OrderByDescending(f => f.Grade)
            .ThenByDescending(f => f.Value)
            .ToList();
    }

    /// <summary>R2: a large share of the weekly quota inside a short sliding window.</summary>
    private static LlmAnomalyFlag? BurnRate(List<LlmUsageRow> rows, LlmQuotaStatus status, LlmAnomalyThresholds t)
    {
        if (!t.BurnEnabled || status.Available <= 0)
        {
            return null;
        }
        // the agent's own available amount, not the rank default: an override moves this threshold with it
        var limit = (long)Math.Ceiling(status.Available * (t.BurnPercent / 100d));
        var span = TimeSpan.FromHours(t.BurnHours);

        var best = 0L;
        TimeSpan bestSpan = default;
        var start = 0;
        var sum = 0L;
        for (var end = 0; end < rows.Count; end++)
        {
            sum += rows[end].QuotaTokens;
            while (rows[end].LocalTimestamp - rows[start].LocalTimestamp >= span)
            {
                sum -= rows[start].QuotaTokens;
                start++;
            }
            if (sum > best)
            {
                best = sum;
                bestSpan = rows[end].LocalTimestamp - rows[start].LocalTimestamp;
            }
        }

        if (best < limit)
        {
            return null;
        }
        var share = status.Available <= 0 ? 1d : (double)best / status.Available;
        return new LlmAnomalyFlag(status.AgentId, status.Codename, LlmAnomalyKind.BurnRate,
            $"{best:N0} Token ({share:P0} des Wochenkontingents) in {Describe(bestSpan)}",
            best,
            share >= 0.9 ? CounterIntelSeverity.High : CounterIntelSeverity.Warning);
    }

    /// <summary>R3: many requests in few minutes AND several of them near-identical. Both conditions, not either.</summary>
    private static LlmAnomalyFlag? Burst(List<LlmUsageRow> rows, LlmQuotaStatus status, LlmAnomalyThresholds t)
    {
        if (!t.BurstEnabled)
        {
            return null;
        }
        var span = TimeSpan.FromMinutes(t.BurstMinutes);
        var start = 0;
        for (var end = 0; end < rows.Count; end++)
        {
            // half-open window [t, t+span)
            while (rows[end].LocalTimestamp - rows[start].LocalTimestamp >= span)
            {
                start++;
            }
            var count = end - start + 1;
            if (count < t.BurstRequests)
            {
                continue;
            }
            var window = rows.GetRange(start, Math.Min(count, MaxBurstRows));
            var duplicates = CountDuplicates(window, t.BurstSimilarityPercent);
            if (duplicates >= t.BurstDuplicates)
            {
                return new LlmAnomalyFlag(status.AgentId, status.Codename, LlmAnomalyKind.Burst,
                    $"{count} Anfragen in {t.BurstMinutes} Minuten, davon {duplicates} nahezu gleichlautend",
                    count, CounterIntelSeverity.High);
            }
        }
        return null;
    }

    /// <summary>How many rows in the window have a near-identical twin.</summary>
    private static int CountDuplicates(List<LlmUsageRow> window, int similarityPercent)
    {
        var flagged = new bool[window.Count];
        for (var i = 0; i < window.Count; i++)
        {
            for (var j = i + 1; j < window.Count; j++)
            {
                if (flagged[i] && flagged[j])
                {
                    continue;
                }
                if (!NearIdentical(window[i], window[j], similarityPercent))
                {
                    continue;
                }
                flagged[i] = true;
                flagged[j] = true;
            }
        }
        return flagged.Count(f => f);
    }

    private static bool NearIdentical(LlmUsageRow a, LlmUsageRow b, int similarityPercent)
    {
        if (!string.IsNullOrEmpty(a.PromptFingerprint)
            && string.Equals(a.PromptFingerprint, b.PromptFingerprint, StringComparison.Ordinal))
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(a.Prompt) || string.IsNullOrWhiteSpace(b.Prompt))
        {
            return false;
        }
        var left = Clip(a.Prompt);
        var right = Clip(b.Prompt);
        var longest = Math.Max(left.Length, right.Length);
        if (longest == 0)
        {
            return true;
        }
        // edit distance, in memory: MySQL cannot translate it, which is why TextSimilarity exists at all
        var distance = TextSimilarity.Distance(left, right);
        return (1d - (double)distance / longest) * 100 >= similarityPercent;
    }

    private static string Clip(string text)
        => text.Length <= MaxPromptCompareChars ? text : text[..MaxPromptCompareChars];

    /// <summary>R4: a week far above the agent's own trailing average, or above their rank's.</summary>
    private static LlmAnomalyFlag? Outlier(
        LlmQuotaStatus status,
        IReadOnlyDictionary<string, double> ownTrailingMean,
        IReadOnlyList<LlmQuotaStatus> statuses,
        LlmAnomalyThresholds t)
    {
        if (!t.OutlierEnabled || status.Consumed <= 0)
        {
            return null;
        }

        if (ownTrailingMean.TryGetValue(status.AgentId, out var own) && own > 0
            && status.Consumed >= own * t.OutlierOwnFactor)
        {
            return new LlmAnomalyFlag(status.AgentId, status.Codename, LlmAnomalyKind.Outlier,
                $"{status.Consumed:N0} Token diese Woche, sonst im Schnitt {own:N0}",
                status.Consumed, CounterIntelSeverity.Warning);
        }

        if (status.Rank is not { } rank)
        {
            return null;
        }
        // leave-one-out, computed here rather than handed in: a mean that includes the judged agent lets a
        // single heavy user in a small rank drag up the very baseline they are measured against
        var peers = statuses.Where(s => s.Rank == rank && s.AgentId != status.AgentId).ToList();
        if (peers.Count < 2)
        {
            return null;
        }
        var mean = peers.Average(s => (double)s.Consumed);
        if (mean <= 0)
        {
            return null;
        }
        return status.Consumed >= mean * t.OutlierRankFactor
            ? new LlmAnomalyFlag(status.AgentId, status.Codename, LlmAnomalyKind.Outlier,
                $"{status.Consumed:N0} Token diese Woche, Rang-Schnitt {mean:N0}",
                status.Consumed, CounterIntelSeverity.Warning)
            : null;
    }

    private static string Describe(TimeSpan span)
        => span.TotalMinutes < 1 ? "unter einer Minute"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min"
            : $"{(int)span.TotalHours} h {span.Minutes} min";
}
