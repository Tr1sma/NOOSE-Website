using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Current "Bester Agent der Woche" schedule config.</summary>
public sealed record TopAgentConfig(bool Enabled, int IntervalDays, bool CreateNote, DateTime? LastRun);

/// <summary>Editable subset of <see cref="TopAgentConfig"/>.</summary>
public sealed record TopAgentConfigInput(bool Enabled, int IntervalDays, bool CreateNote);

/// <summary>Periodically announces the top 3 agents of the interval to the Discord announcements channel (pinging @NOOSE)
/// and optionally files a positive personnel entry for each. Schedule + toggles live in SystemSettings.</summary>
public interface ITopAgentAwardService
{
    Task<TopAgentConfig> GetConfigAsync(CancellationToken cancellationToken = default);
    Task SaveConfigAsync(TopAgentConfigInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Run if enabled and the interval has elapsed since the last run; returns true if agents were announced.</summary>
    Task<bool> RunDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    /// <summary>Force a run now (leadership only), ignoring enabled/interval; returns the number of agents announced.</summary>
    Task<int> RunNowAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ITopAgentAwardService" />
public sealed class TopAgentAwardService(
    IDbContextFactory<AppDbContext> dbFactory,
    IGamificationService gamification,
    IDiscordWebhookService discord) : ITopAgentAwardService
{
    private const int DefaultIntervalDays = 7;
    private const int TopN = 3;
    private static readonly string[] Medals = { "🥇", "🥈", "🥉" };

    public async Task<TopAgentConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await ReadConfigAsync(db, cancellationToken);
    }

    public async Task SaveConfigAsync(TopAgentConfigInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        var interval = input.IntervalDays > 0 ? input.IntervalDays : DefaultIntervalDays;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SetAsync(db, SystemSettingKeys.BestAgentEnabled, input.Enabled ? "true" : "false", cancellationToken);
        await SetAsync(db, SystemSettingKeys.BestAgentIntervalDays, interval.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await SetAsync(db, SystemSettingKeys.BestAgentCreateNote, input.CreateNote ? "true" : "false", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RunDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken);
        if (!config.Enabled)
        {
            return false;
        }
        if (config.LastRun is { } last && (nowUtc - last).TotalDays < config.IntervalDays)
        {
            return false; // interval not yet elapsed
        }
        var count = await AwardAsync(config, actor: null, cancellationToken);
        await StampLastRunAsync(nowUtc, cancellationToken); // advance the cadence even when there were no eligible agents
        return count > 0;
    }

    public async Task<int> RunNowAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        var config = await GetConfigAsync(cancellationToken);
        var count = await AwardAsync(config, actor, cancellationToken);
        await StampLastRunAsync(DateTime.UtcNow, cancellationToken);
        return count;
    }

    private async Task<int> AwardAsync(TopAgentConfig config, ClaimsPrincipal? actor, CancellationToken cancellationToken)
    {
        var top = await gamification.GetLeaderboardAsync(config.IntervalDays, TopN, cancellationToken);
        if (top.Count == 0)
        {
            return 0;
        }

        var localNow = DateTime.Now;
        var kw = ISOWeek.GetWeekOfYear(localNow);
        var isoYear = ISOWeek.GetYear(localNow);

        var sb = new StringBuilder();
        sb.Append("🏆 **Beste Agenten der Woche (KW ").Append(kw).Append(")**");
        for (var i = 0; i < top.Count; i++)
        {
            var medal = i < Medals.Length ? Medals[i] : $"{i + 1}.";
            sb.Append('\n').Append(medal).Append(' ').Append(top[i].Codename)
                .Append(" — ").Append(top[i].Points).Append(" Punkte");
        }
        await discord.PushCustomAsync(NotificationType.Announcement, sb.ToString(), "/bestenliste", cancellationToken);

        if (config.CreateNote)
        {
            await FileNotesAsync(top, kw, isoYear, actor?.GetCodename() ?? "System", cancellationToken);
        }
        return top.Count;
    }

    private async Task FileNotesAsync(
        IReadOnlyList<Models.Gamification.LeaderboardEntry> top, int kw, int isoYear, string author, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var marker = $"KW {kw}/{isoYear}";
        var added = false;
        for (var i = 0; i < top.Count; i++)
        {
            var agentId = top[i].AgentId;
            // idempotent within the ISO week: never file a second entry for the same agent + week
            var already = await db.AgentNotes.AnyAsync(
                n => n.AgentId == agentId && n.Kind == AgentNoteKind.Commendation && n.Text.Contains(marker), cancellationToken);
            if (already)
            {
                continue;
            }
            db.AgentNotes.Add(new AgentNote
            {
                AgentId = agentId,
                Kind = AgentNoteKind.Commendation,
                EntryDate = DateTime.Now,
                Text = $"<p>Bester Agent der Woche {marker} — Platz {i + 1} ({top[i].Points} Punkte).</p>",
                AuthorName = author,
            });
            added = true;
        }
        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<TopAgentConfig> ReadConfigAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            SystemSettingKeys.BestAgentEnabled, SystemSettingKeys.BestAgentIntervalDays,
            SystemSettingKeys.BestAgentCreateNote, SystemSettingKeys.BestAgentLastRun,
        };
        var map = await db.SystemSettings.Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var enabled = Eq(map.GetValueOrDefault(SystemSettingKeys.BestAgentEnabled), "true");
        var interval = int.TryParse(map.GetValueOrDefault(SystemSettingKeys.BestAgentIntervalDays), out var n) && n > 0
            ? n : DefaultIntervalDays;
        // note filing defaults ON; only an explicit "false" disables it
        var createNote = !Eq(map.GetValueOrDefault(SystemSettingKeys.BestAgentCreateNote), "false");
        DateTime? lastRun = DateTime.TryParse(map.GetValueOrDefault(SystemSettingKeys.BestAgentLastRun),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;

        return new TopAgentConfig(enabled, interval, createNote, lastRun);
    }

    private async Task StampLastRunAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SetAsync(db, SystemSettingKeys.BestAgentLastRun, nowUtc.ToString("o", CultureInfo.InvariantCulture), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool Eq(string? value, string other) => string.Equals(value, other, StringComparison.OrdinalIgnoreCase);

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
