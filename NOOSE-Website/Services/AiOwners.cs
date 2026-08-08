using Microsoft.Extensions.Configuration;

namespace NOOSE_Website.Services;

/// <summary>Resolves the configured AI-owner Discord IDs (single Ki:OwnerDiscordId and list Ki:OwnerDiscordIds).</summary>
/// <remarks>An axis of its own rather than a reuse of the bootstrap admins: those exist to keep an account
/// un-lockable and to gate demo mode, and a deployment may well have several of them. Who may hand out AI
/// budget is a separate, narrower question.</remarks>
public static class AiOwners
{
    /// <summary>All configured AI-owner Discord IDs, trimmed and de-duplicated.</summary>
    public static HashSet<string> Ids(IConfiguration configuration)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        var single = configuration["Ki:OwnerDiscordId"];
        if (!string.IsNullOrWhiteSpace(single))
        {
            ids.Add(single.Trim());
        }

        foreach (var id in configuration.GetSection("Ki:OwnerDiscordIds").Get<string[]>() ?? [])
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id.Trim());
            }
        }

        return ids;
    }

    /// <summary>True if the Discord ID is a configured AI owner.</summary>
    public static bool Contains(IConfiguration configuration, string? discordId)
        => !string.IsNullOrWhiteSpace(discordId) && Ids(configuration).Contains(discordId.Trim());
}
