using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities;

namespace NOOSE_Website.Services;

/// <summary>Single source for the agent profile picture URL, its visibility and the initials fallback; no component or endpoint decides either itself.</summary>
public static class AgentAvatar
{
    /// <summary>Serving route for a stored picture; null when the agent has none.</summary>
    public static string? Url(string? fileName)
        => string.IsNullOrWhiteSpace(fileName) ? null : $"/dateien/agenten/profilbild/{fileName}";

    /// <summary>Content type to serve, or null when this viewer may not have the file: a released
    /// picture is open to every agent, a staged one only to its owner and to leadership deciding on it.</summary>
    public static string? ServableContentType(Agent owner, string fileName, ClaimsPrincipal viewer)
    {
        if (owner.AvatarFileName == fileName)
        {
            return owner.AvatarContentType ?? "application/octet-stream";
        }
        if (owner.PendingAvatarFileName == fileName
            && (viewer.GetAgentId() == owner.Id || viewer.IsLeadership()))
        {
            return owner.PendingAvatarContentType ?? "application/octet-stream";
        }
        return null;
    }

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
