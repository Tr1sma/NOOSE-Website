using System.Security.Claims;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Posts notifications to per-category Discord channel webhooks. Best-effort: never throws into the caller.</summary>
public interface IDiscordWebhookService
{
    /// <summary>Post to the channel mapped to this category; no-op if disabled or no URL configured. Optional user ping.</summary>
    Task PushAsync(NotificationType type, string title, string? href, string? mentionDiscordId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Current routing config (fresh read) for the admin page.</summary>
    Task<DiscordWebhookConfig> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Persist the routing config; admin-only.</summary>
    Task SaveConfigAsync(DiscordWebhookConfigInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Send a test message to a category's channel; admin-only. Returns true on 2xx.</summary>
    Task<bool> TestAsync(NotificationType type, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
