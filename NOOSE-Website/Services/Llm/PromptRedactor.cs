using NOOSE_Website.Models.Llm;

namespace NOOSE_Website.Services;

/// <summary>Guards what leaves the building for the external AI endpoint. The primary defence is the caller only
/// assembling visibility-filtered, codename-safe context; this is the last gate on top of that.</summary>
public static class PromptRedactor
{
    /// <summary>Hard cap so a huge paste cannot blow the token budget or exfiltrate a whole dataset.</summary>
    public const int MaxContextChars = 8000;

    /// <summary>Trim + clip context text.</summary>
    public static string Clip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        text = text.Trim();
        return text.Length > MaxContextChars ? text[..MaxContextChars] + " […]" : text;
    }

    /// <summary>Classified/VS content may only leave the building when explicitly allowed in config.</summary>
    public static void GuardClassified(bool isClassified, LlmOptions options)
    {
        if (isClassified && !options.AllowClassifiedContent)
        {
            throw new InvalidOperationException(
                "Verschlusssache: dieser Inhalt darf nicht an den externen KI-Endpunkt gesendet werden.");
        }
    }
}
