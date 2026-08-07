using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Why NOOSEI was called; the quota subsystem bills and reports per feature.</summary>
public enum LlmFeature
{
    /// <summary>Structured record brief on a detail page.</summary>
    Brief = 0,

    /// <summary>Free conversation on the NOOSEI page.</summary>
    Chat = 1,

    /// <summary>Spelling and grammar correction in a rich-text editor.</summary>
    Proofread = 2,

    /// <summary>Composing text from an instruction in a rich-text editor.</summary>
    Compose = 3,
}

/// <summary>German labels and icons of <see cref="LlmFeature"/>.</summary>
public static class LlmFeatureDisplay
{
    public static readonly LlmFeature[] All =
        [LlmFeature.Brief, LlmFeature.Chat, LlmFeature.Proofread, LlmFeature.Compose];

    public static string Name(LlmFeature feature) => feature switch
    {
        LlmFeature.Brief => "Kurzbrief",
        LlmFeature.Chat => "Chat",
        LlmFeature.Proofread => "Rechtschreibung",
        LlmFeature.Compose => "Formulieren",
        _ => feature.ToString(),
    };

    public static string Icon(LlmFeature feature) => feature switch
    {
        LlmFeature.Brief => Icons.Material.Filled.AutoAwesome,
        LlmFeature.Chat => Icons.Material.Filled.Forum,
        LlmFeature.Proofread => Icons.Material.Filled.Spellcheck,
        LlmFeature.Compose => Icons.Material.Filled.EditNote,
        _ => Icons.Material.Filled.SmartToy,
    };
}
