namespace NOOSE_Website.Services;

/// <summary>Single source for the agent profile picture URL and the initials fallback; no component builds either itself.</summary>
public static class AgentAvatar
{
    /// <summary>Serving route for a stored picture; null when the agent has none.</summary>
    public static string? Url(string? fileName)
        => string.IsNullOrWhiteSpace(fileName) ? null : $"/dateien/agenten/profilbild/{fileName}";

    /// <summary>Up to two letters from the codename, shown whenever there is no picture.</summary>
    public static string Initials(string? codename)
    {
        if (string.IsNullOrWhiteSpace(codename))
        {
            return "?";
        }
        var parts = codename.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));
        }
        var word = parts[0];
        return word.Length >= 2 ? word[..2].ToUpperInvariant() : word[..1].ToUpperInvariant();
    }
}
