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
        // Fremde Taskforces gar nicht erst auflösen → erscheinen als „(nicht verfügbar)"-Chip.
        var map = await RecordsReference.ResolveAsync(db, refs, cancellationToken, mayAllTaskforces: isLeadership, meId: meId);
        return Segment(text, tokens, map, isLeadership);
    }

    public async Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> ResolveManyAsync(IReadOnlyList<string?> texts, bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        // Alle Tokens aller Texte einmal sammeln und in EINER Sammelabfrage auflösen.
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
            // Fremde Taskforces gar nicht erst auflösen (s. AufloesenAsync).
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
                // Verschlusssache, die der Betrachter nicht sehen darf → neutraler Chip ohne Name/Link.
                segments.Add(a.Classified && !isLeadership
                    ? new MentionSegment(true, "Verschlusssache", tok.Type, null, Hidden: true)
                    : new MentionSegment(true, a.Display, tok.Type, a.Href, false));
            }
            else
            {
                // Ziel gelöscht/unbekannt.
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

        // 1) Akten (Person/Fraktion/Gruppe/Partei/Operation/Taskforce) über die Schnellsuche – VS-gefiltert;
        //    Taskforces zusätzlich Mitgliedschafts-gefiltert (meId).
        var records = await search.QuickSearchAsync(s, mayClassifiedRead, meId, 8, cancellationToken);
        hit.AddRange(records.Select(a => new MentionHit(a.Category, a.TargetId, a.Name, a.CaseNumber)));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // 2) Agenten – Codename (Klarname nur für die Führung; die Nur-Lese-Aufsicht sieht ihn NICHT, auch wenn
        //    sie VS lesen darf). darfKlarname kommt daher getrennt vom VS-Lese-Flag herein.
        var agents = await db.Users
            .Where(u => u.Status == AgentStatus.Active && !u.IsTeamLead
                && (u.Codename.Contains(s) || (mayRealName && u.RealName != null && u.RealName.Contains(s))))
            .OrderBy(u => u.Codename).Take(CandidatesPerGroup)
            .Select(u => new { u.Id, u.Codename, u.RealName }).ToListAsync(cancellationToken);
        hit.AddRange(agents.Select(a => new MentionHit(nameof(Agent), a.Id,
            string.IsNullOrWhiteSpace(a.Codename) ? "(unbenannter Agent)" : a.Codename,
            mayRealName ? a.RealName : null)));

        // 3) Quellen – Titel-Suche; Sichtbarkeit über die Eltern-Akte (Verschlusssache + Existenz).
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
                    continue; // Eltern-Akte fehlt/Papierkorb oder Verschlusssache ohne Berechtigung.
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
