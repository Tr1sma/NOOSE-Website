using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPlatzhalterService" />
public partial class PlaceholderService(IDbContextFactory<AppDbContext> dbFactory) : IPlaceholderService
{
    public IReadOnlyList<(string Token, string Description)> AvailablePlaceholder { get; } = new[]
    {
        ("{{Name}}", "Name der Akte, an die das Dokument gehängt wird"),
        ("{{Aktenzeichen}}", "Aktenzeichen dieser Akte"),
        ("{{Datum}}", "Aktuelles Datum (TT.MM.JJJJ)"),
        ("{{Uhrzeit}}", "Aktuelle Uhrzeit (HH:MM)"),
        ("{{Agent}}", "Dein Codename"),
        ("{{Dienstgrad}}", "Dein Dienstgrad"),
    };

    public async Task<string> ApplyAsync(string html, string? entityType, string? entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html ?? string.Empty;
        }

        var name = string.Empty;
        var caseNumber = string.Empty;

        if (!string.IsNullOrWhiteSpace(entityType) && !string.IsNullOrWhiteSpace(entityId))
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // resolve visible records only (no classified names for non-leadership)
            if (await Visibility.IsRecordVisibleAsync(db, entityType!, entityId!, actor.MayClassifiedRead(), cancellationToken))
            {
                var record = await RecordNameAsync(db, entityType!, entityId!, cancellationToken);
                name = record.Name;
                caseNumber = record.CaseNumber;
            }
        }

        var now = DateTime.Now;
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = name,
            ["Aktenzeichen"] = caseNumber,
            ["Datum"] = now.ToString("dd.MM.yyyy"),
            ["Uhrzeit"] = now.ToString("HH:mm"),
            ["Agent"] = actor.GetCodename() ?? string.Empty,
            ["Dienstgrad"] = actor.GetRank() is { } dg ? RankDisplay.Name(dg) : string.Empty,
        };

        // HTML-encode values; leave unknown tokens untouched
        return TokenRegex().Replace(html, m =>
        {
            var key = m.Groups[1].Value;
            return replacements.TryGetValue(key, out var value)
                ? System.Net.WebUtility.HtmlEncode(value)
                : m.Value;
        });
    }

    private static async Task<(string Name, string CaseNumber)> RecordNameAsync(AppDbContext db, string type, string id, CancellationToken ct)
    {
        switch (type)
        {
            case nameof(Person):
            {
                var x = await db.People.Where(p => p.Id == id).Select(p => new { p.Name, p.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(Faction):
            {
                var x = await db.Factions.Where(f => f.Id == id).Select(f => new { f.Name, f.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(PersonGroup):
            {
                var x = await db.PersonGroups.Where(g => g.Id == id).Select(g => new { g.Name, g.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(Party):
            {
                var x = await db.Parties.Where(p => p.Id == id).Select(p => new { p.Name, p.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(Operation):
            {
                var x = await db.Operations.Where(o => o.Id == id).Select(o => new { Name = o.Title, o.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(Taskforce):
            {
                var x = await db.Taskforces.Where(t => t.Id == id).Select(t => new { t.Name, t.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(Case):
            {
                var x = await db.Cases.Where(v => v.Id == id).Select(v => new { Name = v.Title, v.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            case nameof(Job):
            {
                var x = await db.Jobs.Where(a => a.Id == id).Select(a => new { Name = a.Title, a.CaseNumber }).FirstOrDefaultAsync(ct);
                return (x?.Name ?? string.Empty, x?.CaseNumber ?? string.Empty);
            }
            default:
                return (string.Empty, string.Empty);
        }
    }

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex TokenRegex();
}
