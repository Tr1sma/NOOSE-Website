using System.Globalization;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Llm;

/// <summary>The AI owner's runtime choice of upstream, plus the quota boost each upstream earns. Stored as JSON in
/// a SystemSetting row — unlike the keys and the models, which stay deploy configuration.</summary>
/// <remarks>Both halves belong together: the boost only means anything relative to an upstream's price, so moving
/// the switch and moving the allowance would otherwise be two edits that must not be made apart.</remarks>
public sealed class LlmProviderSettings
{
    /// <summary>Upstream to route through; null leaves the deployment's own default in charge.</summary>
    public LlmProvider? Active { get; set; }

    /// <summary>Extra share on top of every weekly quota while that upstream is active, keyed by
    /// <see cref="ProviderKey"/> (0 = the configured quota unchanged).</summary>
    public Dictionary<string, int> BoostPercent { get; set; } = new();

    /// <summary>A boost may at most quintuple a quota; beyond that a wrong digit costs real money.</summary>
    public const int MaxBoostPercent = 400;

    /// <summary>Stable key of an upstream (the int, so JSON round-trips cleanly).</summary>
    public static string ProviderKey(LlmProvider provider) => ((int)provider).ToString(CultureInfo.InvariantCulture);

    /// <summary>Boost of one upstream; an unconfigured one gets none.</summary>
    public int BoostFor(LlmProvider provider)
        => BoostPercent.TryGetValue(ProviderKey(provider), out var percent) ? Math.Clamp(percent, 0, MaxBoostPercent) : 0;

    public void SetBoost(LlmProvider provider, int percent)
        => BoostPercent[ProviderKey(provider)] = percent;
}

/// <summary>What the rest of the site reads: the upstream requests actually take, and the boost it carries.</summary>
public sealed record LlmProviderState(LlmProvider Active, int BoostPercent);
