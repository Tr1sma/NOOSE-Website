using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IVerknuepfungVorschlagService" />
public class LinkSuggestionService(IDbContextFactory<AppDbContext> dbFactory) : ILinkSuggestionService
{
    private const int MaxSuggestions = 12;

    public async Task<List<LinkSuggestion>> GetSuggestionsAsync(string entityType, string entityId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        // Block A: nur Personen-Akten liefern Vorschläge (die Signale sind personenzentriert).
        if (entityType != nameof(Person))
        {
            return new();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var isLeadership = viewer.IsLeadership();

        // Kandidaten-Personen → Menge der zutreffenden Begründungen.
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

        // ---- Signal 1: gleiche Telefonnummer (normalisiert auf Ziffern) ----
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

        // ---- Signal 2: gleiche Fraktion ----
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

        // ---- Signal 3: gleiche Personengruppe ----
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

        // ---- Signal 4: gemeinsamer Tag ----
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

        // ---- Signal 5: gemeinsame Verknüpfung (gleicher Nachbar im Verknüpfungs-Graph) ----
        // Bereits (manuell) verknüpfte/bezogene Personen werden zugleich für den Ausschluss gesammelt.
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

        // ---- Signal 6: gleicher Nachname / Zweitname / Alias ----
        // Namen sind ein einzelnes Freitext-Feld → in Wörter zerlegt. Der Vorname (erstes Wort) wird
        // ignoriert (zu unspezifisch); verglichen werden Nachname + weitere Namensteile sowie Aliase.
        // Aus Sicht DIESER Akte je Token die treffendste Begründung (Priorität Nachname > Namensteil > Alias).
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
                tokenReason.TryAdd(key, reason); // erste Eintragung gewinnt → Reihenfolge unten = Priorität
            }
        }

        var ownNameParts = Words(ownName).Skip(1).ToList(); // Vorname raus
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
            // Kandidaten-Namen (Vorname auch hier ignorieren, symmetrisch zum eigenen).
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
            // Kandidaten-Aliase (alle Wörter – ein Alias kennt keinen „Vornamen").
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

        // Bereits typisierte Person-Beziehungen ebenfalls vom Vorschlag ausschließen.
        var related = await db.PersonRelations
            .Where(b => b.PersonAId == entityId || b.PersonBId == entityId)
            .Select(b => new { b.PersonAId, b.PersonBId }).ToListAsync(cancellationToken);
        foreach (var b in related)
        {
            alreadyLinked.Add(b.PersonAId == entityId ? b.PersonBId : b.PersonAId);
        }

        // Kandidaten ohne bereits bestehende Verknüpfung/Beziehung.
        var ids = candidates.Keys.Where(p => !alreadyLinked.Contains(p)).ToList();
        if (ids.Count == 0)
        {
            return new();
        }

        // Auflösen + Sichtbarkeit (Verschlusssache nur für Führung; Papierkorb via globalem Filter).
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

    /// <summary>Reduziert eine Telefonnummer auf ihre Ziffern (toleriert Formatierungen/Leerzeichen).</summary>
    private static string Normalize(string? number)
        => string.IsNullOrEmpty(number) ? string.Empty : new string(number.Where(char.IsDigit).ToArray());

    /// <summary>Zerlegt einen Namen/Alias in Wörter (Trennung an Leerzeichen, Bindestrich, Schrägstrich, Komma, Punkt …).</summary>
    private static List<string> Words(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? new()
            : text.Split(new[] { ' ', '\t', '\n', '\r', '-', '/', ',', '.', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Normalisiert ein Namens-Wort für den Abgleich (nur Buchstaben/Ziffern, klein – toleriert Umlaute).</summary>
    private static string NormToken(string word)
        => new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>Häufige Artikel/Partikel, die als alleiniger Namens-/Alias-Treffer nur Rauschen erzeugen würden.</summary>
    private static readonly HashSet<string> StopWords = new()
    {
        "der", "die", "das", "den", "dem", "von", "van", "de", "el", "la", "le", "the", "of", "und", "and",
    };
}
