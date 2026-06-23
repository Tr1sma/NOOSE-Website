using Microsoft.Extensions.Configuration;

namespace NOOSE_Website.Services;

/// <summary>Resolves configured bootstrap-admin Discord IDs (single Bootstrap:AdminDiscordId and list Bootstrap:AdminDiscordIds).</summary>
public static class BootstrapAdmins
{
    /// <summary>All configured bootstrap-admin Discord IDs, trimmed and de-duplicated.</summary>
    public static HashSet<string> Ids(IConfiguration configuration)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        var single = configuration["Bootstrap:AdminDiscordId"];
        if (!string.IsNullOrWhiteSpace(single))
        {
            ids.Add(single.Trim());
        }

        foreach (var id in configuration.GetSection("Bootstrap:AdminDiscordIds").Get<string[]>() ?? [])
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id.Trim());
            }
        }

        return ids;
    }

    /// <summary>True if the Discord ID is a configured bootstrap admin.</summary>
    public static bool Contains(IConfiguration configuration, string? discordId)
        => !string.IsNullOrWhiteSpace(discordId) && Ids(configuration).Contains(discordId.Trim());
}
