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

    public async Task PushAsync(NotificationType type, string? href, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!DiscordRouting.IsRoutable(type))
            {
                return;
            }
            var config = await GetCachedConfigAsync(cancellationToken);
            if (!config.Enabled || !config.Webhooks.TryGetValue(type, out var url) || string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            await SendAsync(url, Compose(type, config.SiteBaseUrl, href), cancellationToken);
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
        foreach (var type in DiscordRouting.RoutableTypes)
        {
            input.Webhooks.TryGetValue(type, out var raw);
            if (Trim(raw) is { } url && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Der Webhook für „{NotificationTypeDisplay.Name(type)}“ muss mit https:// beginnen.");
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SetAsync(db, SystemSettingKeys.DiscordEnabled, input.Enabled ? "true" : "false", cancellationToken);
        await SetAsync(db, SystemSettingKeys.SiteBaseUrl, baseUrl, cancellationToken);
        foreach (var type in DiscordRouting.RoutableTypes)
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

    // content is app-authored and identity-free; suppress every mention so no <@id> in any field can ping
    private async Task<bool> SendAsync(string url, string content, CancellationToken cancellationToken)
    {
        var payload = new
        {
            content,
            username = "NOOSE",
            allowed_mentions = new { parse = Array.Empty<string>() },
        };
        var client = httpFactory.CreateClient("discord");
        using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Discord webhook returned {Status}.", (int)response.StatusCode);
        }
        return response.IsSuccessStatusCode;
    }

    // generic per-category notice + login-gated link; NEVER the in-app title (which may carry names/notes/VS content)
    private static string Compose(NotificationType type, string baseUrl, string? href)
    {
        var sb = new StringBuilder(Notice(type));
        var link = Link(baseUrl, href);
        if (link is not null)
        {
            sb.Append('\n').Append(link);
        }
        return sb.ToString();
    }

    private static string Notice(NotificationType type) => type switch
    {
        NotificationType.Announcement => "📢 Neue Ankündigung im Schwarzen Brett.",
        NotificationType.Followup => "⏰ Eine Wiedervorlage ist fällig.",
        NotificationType.SituationReport => "📊 Ein neuer Lagebericht liegt vor.",
        NotificationType.Recruiting => "📝 Neue Aktivität im Bewerbungswesen.",
        NotificationType.Mention => "💬 Es gibt eine neue Erwähnung in einer Akte.",
        _ => "🔔 Neue Benachrichtigung.",
    };

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
        foreach (var type in DiscordRouting.RoutableTypes)
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
