using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IMentionService" />
public class MentionService(IDbContextFactory<AppDbContext> dbFactory, ISearchService search) : IMentionService
{
    private const int CandidatesPerGroup = 5;

    public async Task<IReadOnlyList<MentionSegment>> ResolveAsync(string? text, bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<MentionSegment>();
        }
        var tokens = MentionParser.Parse(text);
        if (tokens.Count == 0)
        {
            return new[] { new MentionSegment(false, text) };
        }

        var refs = tokens.Select(t => (t.Type, t.Id)).Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // foreign taskforces stay unresolved -> shown as unavailable chip
        var map = await RecordsReference.ResolveAsync(db, refs, cancellationToken, mayAllTaskforces: isLeadership, meId: meId);
        return Segment(text, tokens, map, isLeadership);
    }

    public async Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> ResolveManyAsync(IReadOnlyList<string?> texts, bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        // collect all tokens from all texts and resolve in one query
        var tokenPerText = texts.Select(MentionParser.Parse).ToList();
        var refs = tokenPerText.SelectMany(ts => ts).Select(t => (t.Type, t.Id)).Distinct().ToList();

        Dictionary<(string, string), RecordsReference.Resolution> map;
        if (refs.Count == 0)
        {
            map = new();
        }
        else
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // foreign taskforces stay unresolved
            map = await RecordsReference.ResolveAsync(db, refs, cancellationToken, mayAllTaskforces: isLeadership, meId: meId);
        }

        var result = new List<IReadOnlyList<MentionSegment>>(texts.Count);
        for (var i = 0; i < texts.Count; i++)
        {
            result.Add(Segment(texts[i] ?? string.Empty, tokenPerText[i], map, isLeadership));
        }
        return result;
    }

    private static List<MentionSegment> Segment(string text, IReadOnlyList<MentionToken> tokens,
        Dictionary<(string, string), RecordsReference.Resolution> map, bool isLeadership)
    {
        if (tokens.Count == 0)
        {
            return new() { new MentionSegment(false, text) };
        }
        var segments = new List<MentionSegment>();
        var pos = 0;
        foreach (var tok in tokens)
        {
            if (tok.Start > pos)
            {
                segments.Add(new MentionSegment(false, text.Substring(pos, tok.Start - pos)));
            }
            if (map.TryGetValue((tok.Type, tok.Id), out var a))
            {
                // classified the viewer can't see -> neutral chip without name/link
                segments.Add(a.Classified && !isLeadership
                    ? new MentionSegment(true, "Verschlusssache", tok.Type, null, Hidden: true)
                    : new MentionSegment(true, a.Display, tok.Type, a.Href, false));
            }
            else
            {
                // target deleted/unknown
                segments.Add(new MentionSegment(true, "(nicht verfügbar)", tok.Type, null, false));
            }
            pos = tok.Start + tok.Length;
        }
        if (pos < text.Length)
        {
            segments.Add(new MentionSegment(false, text.Substring(pos)));
        }
        return segments;
    }

    public async Task<List<MentionHit>> CandidatesAsync(string? text, bool mayClassifiedRead, bool mayRealName, string? meId, CancellationToken cancellationToken = default)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return new();
        }

        var hit = new List<MentionHit>();

        // records via quick search, classification- and taskforce-membership-filtered
        var records = await search.QuickSearchAsync(s, new ViewerScope(mayClassifiedRead, mayClassifiedRead, meId, null), 8, cancellationToken);
        hit.AddRange(records.Select(a => new MentionHit(a.Category, a.TargetId, a.Name, a.CaseNumber)));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // real name gated by mayRealName, kept separate from the classified-read flag
        var agents = await db.Users
            .Where(u => u.Status == AgentStatus.Active && !u.IsTeamLead
                && (u.Codename.Contains(s) || (mayRealName && u.RealName != null && u.RealName.Contains(s))))
            .OrderBy(u => u.Codename).Take(CandidatesPerGroup)
            .Select(u => new { u.Id, u.Codename, u.RealName }).ToListAsync(cancellationToken);
        hit.AddRange(agents.Select(a => new MentionHit(nameof(Agent), a.Id,
            string.IsNullOrWhiteSpace(a.Codename) ? "(unbenannter Agent)" : a.Codename,
            mayRealName ? a.RealName : null)));

        // sources by title; visibility derived from the parent record
        var sourcesRaw = await db.Sources.Where(q => q.Title.Contains(s))
            .OrderByDescending(q => q.ModifiedAt ?? q.CreatedAt).Take(20)
            .Select(q => new { q.Id, q.Title, q.EntityType, q.EntityId }).ToListAsync(cancellationToken);
        if (sourcesRaw.Count > 0)
        {
            var parentsRefs = sourcesRaw.Select(q => (q.EntityType, q.EntityId)).Distinct().ToList();
            var parentsMap = await RecordsReference.ResolveAsync(db, parentsRefs, cancellationToken);
            var count = 0;
            foreach (var q in sourcesRaw)
            {
                if (!parentsMap.TryGetValue((q.EntityType, q.EntityId), out var e) || (e.Classified && !mayClassifiedRead))
                {
                    continue; // parent missing/trashed or classified without access
                }
                hit.Add(new MentionHit(nameof(Source), q.Id, string.IsNullOrWhiteSpace(q.Title) ? "Quelle" : q.Title, e.Display));
                if (++count >= CandidatesPerGroup)
                {
                    break;
                }
            }
        }

        return hit;
    }
}
