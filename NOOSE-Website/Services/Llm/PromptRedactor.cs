using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Guards what leaves the building for the external AI endpoint. The primary defence is the caller only
/// assembling visibility-filtered, codename-safe context; this is the last gate on top of that.</summary>
public static class PromptRedactor
{
    /// <summary>Hard cap so a huge paste cannot blow the token budget or exfiltrate a whole dataset.</summary>
    public const int MaxContextChars = 16_000;

    /// <summary>One tool result handed back to the model.</summary>
    public const int MaxToolResultChars = 6_000;

    /// <summary>What an agent may type into the assistant in one turn.</summary>
    public const int MaxChatInputChars = 4_000;

    /// <summary>Trim + clip context text.</summary>
    public static string Clip(string? text) => Clip(text, MaxContextChars);

    /// <summary>Trim + clip to an explicit budget.</summary>
    public static string Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        text = text.Trim();
        return text.Length > max ? text[..max] + " […]" : text;
    }

    /// <summary>Classified/VS content may only leave the building when explicitly allowed in config.</summary>
    public static void GuardClassified(bool isClassified, LlmOptions options)
    {
        if (isClassified && !options.AllowClassifiedEgress)
        {
            throw new InvalidOperationException(
                "Verschlusssache: dieser Inhalt darf nicht an den externen KI-Endpunkt gesendet werden.");
        }
    }
}
