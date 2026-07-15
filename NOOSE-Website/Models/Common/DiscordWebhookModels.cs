using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Which notification categories are safe to mirror to a shared Discord channel (broadcast events only, never per-recipient personal notifications).</summary>
public static class DiscordRouting
{
    public static readonly IReadOnlyList<NotificationType> RoutableTypes = new[]
    {
        NotificationType.Announcement,
        NotificationType.Followup,
        NotificationType.SituationReport,
        NotificationType.Recruiting,
        NotificationType.Mention,
    };

    public static bool IsRoutable(NotificationType type) => RoutableTypes.Contains(type);
}

/// <summary>Snapshot of the Discord webhook routing config (master switch, base URL, per-category channel URLs).</summary>
public sealed record DiscordWebhookConfig(
    bool Enabled, string SiteBaseUrl, IReadOnlyDictionary<NotificationType, string?> Webhooks)
{
    /// <summary>Default site base for absolute links in Discord messages when unset.</summary>
    public const string DefaultBaseUrl = "https://noose.info";
}

/// <summary>Admin input for the Discord webhook config page.</summary>
public sealed class DiscordWebhookConfigInput
{
    public bool Enabled { get; set; }
    public string? SiteBaseUrl { get; set; }

    /// <summary>Webhook URL per category; empty/null disables that category.</summary>
    public Dictionary<NotificationType, string?> Webhooks { get; set; } = new();
}
