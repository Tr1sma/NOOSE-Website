using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ILinkSuggestionService" />
public class LinkSuggestionService(IDbContextFactory<AppDbContext> dbFactory) : ILinkSuggestionService
{
    private const int MaxSuggestions = 12;

    public async Task<List<LinkSuggestion>> GetSuggestionsAsync(string entityType, string entityId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        // only person records yield suggestions; the signals are person-centric
        if (entityType != nameof(Person))
        {
            return new();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var isLeadership = viewer.IsLeadership();

        // candidate person -> set of matching reasons
        var candidates = new Dictionary<string, HashSet<string>>();
        void Add(string personId, string reason)
        {
            if (string.IsNullOrEmpty(personId) || personId == entityId)
            {
                return;
            }
            if (!candidates.TryGetValue(personId, out var reasons))
            {
                reasons = new();
                candidates[personId] = reasons;
            }
            reasons.Add(reason);
        }

        // ---- signal 1: same phone number (digits only) ----
        var ownNumbers = await db.PersonPhones.Where(t => t.PersonId == entityId)
            .Select(t => t.Number).ToListAsync(cancellationToken);
        var ownNumbersNorm = ownNumbers.Select(Normalize).Where(n => n.Length > 0).ToHashSet();
        if (ownNumbersNorm.Count > 0)
        {
            var other = await db.PersonPhones.Where(t => t.PersonId != entityId)
                .Select(t => new { t.PersonId, t.Number }).ToListAsync(cancellationToken);
            foreach (var t in other)
            {
                if (ownNumbersNorm.Contains(Normalize(t.Number)))
                {
                    Add(t.PersonId, $"gleiche Telefonnummer ({t.Number})");
                }
            }
        }

        // ---- signal 2: same faction ----
        var factionIds = await db.FactionMembers.Where(m => m.PersonId == entityId)
            .Select(m => m.FactionId).Distinct().ToListAsync(cancellationToken);
        if (factionIds.Count > 0)
        {
            var names = await db.Factions.Where(f => factionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name }).ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);
            var members = await db.FactionMembers
                .Where(m => m.PersonId != entityId && factionIds.Contains(m.FactionId))
                .Select(m => new { m.PersonId, m.FactionId }).ToListAsync(cancellationToken);
            foreach (var m in members)
            {
                Add(m.PersonId, $"gleiche Fraktion: {names.GetValueOrDefault(m.FactionId, "?")}");
            }
        }

        // ---- signal 3: same person group ----
        var groupsIds = await db.PersonGroupMembers.Where(m => m.PersonId == entityId)
            .Select(m => m.PersonGroupId).Distinct().ToListAsync(cancellationToken);
        if (groupsIds.Count > 0)
        {
            var names = await db.PersonGroups.Where(g => groupsIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name }).ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
            var members = await db.PersonGroupMembers
                .Where(m => m.PersonId != entityId && groupsIds.Contains(m.PersonGroupId))
                .Select(m => new { m.PersonId, m.PersonGroupId }).ToListAsync(cancellationToken);
            foreach (var m in members)
            {
                Add(m.PersonId, $"gleiche Gruppe: {names.GetValueOrDefault(m.PersonGroupId, "?")}");
            }
        }

        // ---- signal 4: shared tag ----
        var tagIds = await db.TagMappings
            .Where(z => z.EntityType == nameof(Person) && z.EntityId == entityId)
            .Select(z => z.TagId).Distinct().ToListAsync(cancellationToken);
        if (tagIds.Count > 0)
        {
            var tagNames = await db.Tags.Where(t => tagIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
            var mappings = await db.TagMappings
                .Where(z => z.EntityType == nameof(Person) && z.EntityId != entityId && tagIds.Contains(z.TagId))
                .Select(z => new { z.EntityId, z.TagId }).ToListAsync(cancellationToken);
            foreach (var z in mappings)
            {
                Add(z.EntityId, $"gemeinsamer Tag: {tagNames.GetValueOrDefault(z.TagId, "?")}");
            }
        }

        // ---- signal 5: shared link (same neighbour in the link graph) ----
        // also collects already-linked persons for exclusion
        var meKey = $"{nameof(Person)}:{entityId}";
        var alreadyLinked = new HashSet<string>();
        var allVk = await db.Links
            .Select(v => new { v.SourceType, v.SourceId, v.TargetType, v.TargetId })
            .ToListAsync(cancellationToken);
        var neighbors = new HashSet<string>();
        foreach (var v in allVk)
        {
            var source = $"{v.SourceType}:{v.SourceId}";
            var target = $"{v.TargetType}:{v.TargetId}";
            if (source == meKey)
            {
                neighbors.Add(target);
                if (v.TargetType == nameof(Person))
                {
                    alreadyLinked.Add(v.TargetId);
                }
            }
            else if (target == meKey)
            {
                neighbors.Add(source);
                if (v.SourceType == nameof(Person))
                {
                    alreadyLinked.Add(v.SourceId);
                }
            }
        }
        foreach (var v in allVk)
        {
            var source = $"{v.SourceType}:{v.SourceId}";
            var target = $"{v.TargetType}:{v.TargetId}";
            if (neighbors.Contains(source) && v.TargetType == nameof(Person) && target != meKey)
            {
                Add(v.TargetId, "gemeinsame Verknüpfung");
            }
            else if (neighbors.Contains(target) && v.SourceType == nameof(Person) && source != meKey)
            {
                Add(v.SourceId, "gemeinsame Verknüpfung");
            }
        }

        // ---- signal 6: same surname / middle name / alias ----
        // first name (first word) ignored as too unspecific; compare surname, other name parts and aliases
        var ownName = await db.People.Where(p => p.Id == entityId)
            .Select(p => p.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var ownAliases = await db.PersonAliases.Where(a => a.PersonId == entityId)
            .Select(a => a.AliasName).ToListAsync(cancellationToken);

        var tokenReason = new Dictionary<string, string>();
        void TokenRemember(string display, string reason)
        {
            var key = NormToken(display);
            if (key.Length >= 2 && !StopWords.Contains(key))
            {
                tokenReason.TryAdd(key, reason); // first entry wins, so order below = priority
            }
        }

        var ownNameParts = Words(ownName).Skip(1).ToList(); // drop first name
        var ownLastName = ownNameParts.LastOrDefault();
        if (ownLastName is not null)
        {
            TokenRemember(ownLastName, $"gleicher Nachname: {ownLastName}");
        }
        foreach (var part in ownNameParts.Where(t => t != ownLastName))
        {
            TokenRemember(part, $"gemeinsamer Namensteil: {part}");
        }
        foreach (var alias in ownAliases)
        {
            foreach (var word in Words(alias))
            {
                TokenRemember(word, $"gemeinsamer Alias: {alias.Trim()}");
            }
        }

        if (tokenReason.Count > 0)
        {
            // candidate names, also dropping the first name symmetrically
            var otherNames = await db.People.Where(p => p.Id != entityId)
                .Select(p => new { p.Id, p.Name }).ToListAsync(cancellationToken);
            foreach (var p in otherNames)
            {
                foreach (var word in Words(p.Name).Skip(1))
                {
                    if (tokenReason.TryGetValue(NormToken(word), out var reason))
                    {
                        Add(p.Id, reason);
                    }
                }
            }
            // candidate aliases (all words; an alias has no first name)
            var otherAliases = await db.PersonAliases.Where(a => a.PersonId != entityId)
                .Select(a => new { a.PersonId, a.AliasName }).ToListAsync(cancellationToken);
            foreach (var a in otherAliases)
            {
                foreach (var word in Words(a.AliasName))
                {
                    if (tokenReason.TryGetValue(NormToken(word), out var reason))
                    {
                        Add(a.PersonId, reason);
                    }
                }
            }
        }

        // also exclude existing typed person relations
        var related = await db.PersonRelations
            .Where(b => b.PersonAId == entityId || b.PersonBId == entityId)
            .Select(b => new { b.PersonAId, b.PersonBId }).ToListAsync(cancellationToken);
        foreach (var b in related)
        {
            alreadyLinked.Add(b.PersonAId == entityId ? b.PersonBId : b.PersonAId);
        }

        // candidates without an existing link/relation
        var ids = candidates.Keys.Where(p => !alreadyLinked.Contains(p)).ToList();
        if (ids.Count == 0)
        {
            return new();
        }

        // resolve + visibility (classified for leadership only)
        var people = await db.People.Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified })
            .ToListAsync(cancellationToken);

        var result = new List<LinkSuggestion>();
        foreach (var p in people)
        {
            if (p.IsClassified && !isLeadership)
            {
                continue;
            }
            var reasons = candidates[p.Id];
            result.Add(new LinkSuggestion(
                nameof(Person), p.Id, p.Name, p.CaseNumber, $"/personen/{p.Id}",
                string.Join(" · ", reasons), reasons.Count));
        }

        return result
            .OrderByDescending(v => v.Strength)
            .ThenBy(v => v.Designation)
            .Take(MaxSuggestions)
            .ToList();
    }

    /// <summary>Reduces a phone number to its digits.</summary>
    private static string Normalize(string? number)
        => string.IsNullOrEmpty(number) ? string.Empty : new string(number.Where(char.IsDigit).ToArray());

    /// <summary>Splits a name/alias into words at whitespace and common punctuation.</summary>
    private static List<string> Words(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? new()
            : text.Split(new[] { ' ', '\t', '\n', '\r', '-', '/', ',', '.', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Normalises a name word for matching (lowercase letters/digits only).</summary>
    private static string NormToken(string word)
        => new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>Common articles/particles that would only produce noise as a sole name/alias match.</summary>
    private static readonly HashSet<string> StopWords = new()
    {
        "der", "die", "das", "den", "dem", "von", "van", "de", "el", "la", "le", "the", "of", "und", "and",
    };
}
