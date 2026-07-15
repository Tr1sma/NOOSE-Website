using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IDiscordWebhookService" />
public class DiscordWebhookService(
    IHttpClientFactory httpFactory,
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache,
    ILogger<DiscordWebhookService> logger) : IDiscordWebhookService
{
    private const string CacheKey = "DiscordWebhookConfig";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    public async Task PushAsync(NotificationType type, string title, string? href, string? mentionDiscordId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await GetCachedConfigAsync(cancellationToken);
            if (!config.Enabled || !config.Webhooks.TryGetValue(type, out var url) || string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            await SendAsync(url, Compose(config.SiteBaseUrl, title, href, mentionDiscordId), cancellationToken);
        }
        catch (Exception ex)
        {
            /* best effort */
            logger.LogWarning(ex, "Discord webhook push for {Type} failed.", type);
        }
    }

    public async Task<DiscordWebhookConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var values = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(e => e.Key, e => e.Value, cancellationToken);
        return Build(values);
    }

    public async Task SaveConfigAsync(DiscordWebhookConfigInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAdmin(actor);

        // light validation: URLs must be https when present
        var baseUrl = Trim(input.SiteBaseUrl);
        if (baseUrl is not null && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Die Basis-URL muss mit https:// beginnen.");
        }
        foreach (var (type, raw) in input.Webhooks)
        {
            var url = Trim(raw);
            if (url is not null && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Der Webhook für „{NotificationTypeDisplay.Name(type)}“ muss mit https:// beginnen.");
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SetAsync(db, SystemSettingKeys.DiscordEnabled, input.Enabled ? "true" : "false", cancellationToken);
        await SetAsync(db, SystemSettingKeys.SiteBaseUrl, baseUrl, cancellationToken);
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            input.Webhooks.TryGetValue(type, out var raw);
            await SetAsync(db, SystemSettingKeys.DiscordWebhookPrefix + type, Trim(raw), cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKey);
    }

    public async Task<bool> TestAsync(NotificationType type, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireAdmin(actor);

        var config = await GetConfigAsync(cancellationToken);
        if (!config.Webhooks.TryGetValue(type, out var url) || string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        return await SendAsync(url,
            $"🔔 Test-Benachrichtigung ({NotificationTypeDisplay.Name(type)}) – die Anbindung funktioniert.",
            cancellationToken);
    }

    private async Task<DiscordWebhookConfig> GetCachedConfigAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out DiscordWebhookConfig? cached) && cached is not null)
        {
            return cached;
        }
        var config = await GetConfigAsync(cancellationToken);
        cache.Set(CacheKey, config, CacheDuration);
        return config;
    }

    // only ping explicit user mentions, never @everyone/@here/roles
    private async Task<bool> SendAsync(string url, string content, CancellationToken cancellationToken)
    {
        var payload = new
        {
            content,
            username = "NOOSE",
            allowed_mentions = new { parse = new[] { "users" } },
        };
        var client = httpFactory.CreateClient("discord");
        using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Discord webhook returned {Status}.", (int)response.StatusCode);
        }
        return response.IsSuccessStatusCode;
    }

    private static string Compose(string baseUrl, string title, string? href, string? mentionDiscordId)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(mentionDiscordId))
        {
            sb.Append("<@").Append(mentionDiscordId).Append("> ");
        }
        sb.Append(title);
        var link = Link(baseUrl, href);
        if (link is not null)
        {
            sb.Append('\n').Append(link);
        }
        return sb.ToString();
    }

    // absolute href as-is; relative href joined onto the configured base
    private static string? Link(string baseUrl, string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }
        return $"{baseUrl.TrimEnd('/')}/{href.TrimStart('/')}";
    }

    private static DiscordWebhookConfig Build(IReadOnlyDictionary<string, string?> values)
    {
        var enabled = string.Equals(values.GetValueOrDefault(SystemSettingKeys.DiscordEnabled), "true", StringComparison.OrdinalIgnoreCase);
        var baseUrl = Trim(values.GetValueOrDefault(SystemSettingKeys.SiteBaseUrl)) ?? DiscordWebhookConfig.DefaultBaseUrl;
        var map = new Dictionary<NotificationType, string?>();
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            map[type] = Trim(values.GetValueOrDefault(SystemSettingKeys.DiscordWebhookPrefix + type));
        }
        return new DiscordWebhookConfig(enabled, baseUrl, map);
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task SetAsync(AppDbContext db, string key, string? value, CancellationToken cancellationToken)
    {
        var row = await db.SystemSettings.FirstOrDefaultAsync(e => e.Key == key, cancellationToken);
        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }
}
