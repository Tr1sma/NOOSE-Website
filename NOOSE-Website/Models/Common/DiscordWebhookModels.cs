using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Which notification categories mirror to a shared Discord channel, and how each pings: personal categories ping the recipients, broadcast categories ping a configured role.</summary>
public static class DiscordRouting
{
    public static readonly IReadOnlyList<NotificationType> RoutableTypes = new[]
    {
        NotificationType.Announcement,
        NotificationType.Followup,
        NotificationType.SituationReport,
        NotificationType.Recruiting,
        NotificationType.Mention,
        NotificationType.JobAssigned,
        NotificationType.JobDueSoon,
        NotificationType.MeetingScheduled,
        NotificationType.MeetingReminder,
        NotificationType.PersonnelEntry,
    };

    public static bool IsRoutable(NotificationType type) => RoutableTypes.Contains(type);

    /// <summary>Category whose Discord post pings the specific recipient agents.</summary>
    public static bool PingsRecipients(NotificationType type)
        // MeetingReminder pings the role, not individuals: a per-person mention list in a shared
        // channel is the complement of the absence set and would leak who filed an absence
        => type is NotificationType.Mention or NotificationType.Followup
                or NotificationType.JobAssigned or NotificationType.JobDueSoon
                or NotificationType.PersonnelEntry;

    /// <summary>Category whose Discord post pings a configured role instead of individuals.</summary>
    public static bool PingsRole(NotificationType type)
        => IsRoutable(type) && !PingsRecipients(type);

    /// <summary>Routable categories that ping a role (need a configurable role id).</summary>
    public static readonly IReadOnlyList<NotificationType> RoleRoutableTypes =
        RoutableTypes.Where(PingsRole).ToArray();

    /// <summary>Default role id per broadcast category; admin-overridable per server.</summary>
    public static string DefaultRole(NotificationType type) => type switch
    {
        NotificationType.Recruiting => "1515098218545938442", // HRB
        _ => "1479854499853238475",                            // NOOSE
    };
}

/// <summary>Snapshot of the Discord webhook routing config (master switch, base URL, per-category channel URLs and role mentions).</summary>
public sealed record DiscordWebhookConfig(
    bool Enabled, string SiteBaseUrl,
    IReadOnlyDictionary<NotificationType, string?> Webhooks,
    IReadOnlyDictionary<NotificationType, string?> Roles)
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

    /// <summary>Role id to ping per broadcast category; empty/null falls back to the default role.</summary>
    public Dictionary<NotificationType, string?> Roles { get; set; } = new();
}
