using System.Security.Claims;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Posts a generic notice plus a login-gated link to a per-category Discord channel, pinging the recipients (personal categories) or a configured role (broadcast categories). Best-effort: never throws into the caller.</summary>
public interface IDiscordWebhookService
{
    /// <summary>Post the category's generic notice (never the in-app title) to its channel and ping the recipients or the configured role; no-op if disabled, unroutable, or no URL configured.</summary>
    Task PushAsync(NotificationType type, string? href, IReadOnlyCollection<string>? recipientAgentIds, CancellationToken cancellationToken = default);

    /// <summary>Current routing config (fresh read) for the admin page.</summary>
    Task<DiscordWebhookConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Persist the routing config; admin-only.</summary>
    Task SaveConfigAsync(DiscordWebhookConfigInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Send a test message to a category's channel; admin-only. Returns true on 2xx.</summary>
    Task<bool> TestAsync(NotificationType type, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
