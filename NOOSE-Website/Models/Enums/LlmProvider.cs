using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Which upstream NOOSEI talks to. Not the same axis as OpenRouter's own provider list
/// (<c>LlmOptions.Providers</c>): that one picks who serves a model *behind* OpenRouter, this one picks whether
/// OpenRouter is in the path at all.</summary>
public enum LlmProvider
{
    /// <summary>OpenRouter's aggregating gateway; reports real cost per call.</summary>
    OpenRouter = 0,

    /// <summary>DeepSeek's own OpenAI-compatible endpoint; cheaper, but reports no cost.</summary>
    DeepSeek = 1,
}

/// <summary>German label and icon of an upstream.</summary>
public static class LlmProviderDisplay
{
    public static readonly LlmProvider[] All = [LlmProvider.OpenRouter, LlmProvider.DeepSeek];

    public static string Name(LlmProvider provider) => provider switch
    {
        LlmProvider.OpenRouter => "OpenRouter",
        LlmProvider.DeepSeek => "DeepSeek",
        _ => provider.ToString(),
    };

    public static string Icon(LlmProvider provider) => provider switch
    {
        LlmProvider.DeepSeek => Icons.Material.Filled.Route,
        _ => Icons.Material.Filled.AltRoute,
    };
}
