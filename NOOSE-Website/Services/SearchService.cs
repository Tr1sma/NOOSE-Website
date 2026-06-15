using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ISearchService" />
public class SearchService(IDbContextFactory<AppDbContext> dbFactory) : ISearchService
{
    private const int MaxPerCategory = 50;

    /// <summary>Obergrenze der in-memory geprüften Fuzzy-Kandidaten je Kategorie (Schutz vor Last bei großen Datenmengen).</summary>
    private const int FuzzyCandidatesMax = 2000;

    public async Task<List<SearchResultGroup>> SearchAsync(SearchCriteria criteria, bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var s = criteria.Text?.Trim();
        var hasText = !string.IsNullOrEmpty(s);
        var tagIds = criteria.TagIds ?? new();
        var hasTags = tagIds.Count > 0;
        var max = criteria.MaxMode;

        // Bewusst KEIN Früh-Ausstieg bei leerem Text/leeren Tags: ohne Filter sollen alle (sichtbaren)
        // Personen erscheinen (Durchblättern). Die Personen-Query unten lässt dann einfach das Text-Where
        // weg; die reinen Text-Kategorien (Doks/Quellen/Kommentare) bleiben mangels Suchtext leer.

        var categories = criteria.Categories is { Count: > 0 } ? criteria.Categories.ToHashSet() : null;
        bool Active(string kat) => categories is null || categories.Contains(kat);

        // Im Max-Modus werden die Inhalts-Kategorien (Doks/Quellen/Kommentare) immer mitdurchsucht,
        // unabhängig davon, ob ihr Häkchen gesetzt ist – eine einzige Wahrheitsquelle für die Erzwingung.
        bool ContentActive(string kat) => max || Active(kat);

        // Suchwörter nur einmal zerlegen (für den in-memory Fuzzy-Pass).
        var searchWords = criteria.Fuzzy && hasText
            ? TextSimilarity.Tokens(s)
            : (IReadOnlyList<string>)Array.Empty<string>();
        bool FuzzyActive(int substringHit) => criteria.Fuzzy && hasText && substringHit < MaxPerCategory;

        var groups = new List<SearchResultGroup>();

        // ---- Personen (Name/Aktenzeichen/Beschreibung/Aliase; Max zusätzlich Steckbrief-Unterdaten) ----
        if (Active(nameof(Person)))
        {
            var q = db.People.Where(p => isLeadership || !p.IsClassified);
            if (hasText)
            {
                q = q.Where(p => p.Name.Contains(s!) || p.CaseNumber.Contains(s!)
                    || (p.Description != null && p.Description.Contains(s!))
                    || p.Aliases.Any(a => a.AliasName.Contains(s!))
                    || (max && (
                           p.PhoneNumbers.Any(t => t.Number.Contains(s!) || (t.Designation != null && t.Designation.Contains(s!)))
                        || p.Vehicles.Any(f => f.Designation.Contains(s!) || (f.LicensePlate != null && f.LicensePlate.Contains(s!)))
                        || p.Locations.Any(o => o.Text.Contains(s!) || (o.Note != null && o.Note.Contains(s!)))
                        || p.Weapons.Any(w => w.Text.Contains(s!)))));
            }
            if (hasTags)
            {
                q = q.Where(p => db.TagMappings.Any(z => z.EntityType == nameof(Person) && z.EntityId == p.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(p => p.Name).Take(MaxPerCategory)
                .Select(p => new SearchHit(nameof(Person), p.Id, p.Name,
                    p.Description ?? string.Empty, p.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.People.Where(p => isLeadership || !p.IsClassified);
                if (hasTags)
                {
                    @base = @base.Where(p => db.TagMappings.Any(z => z.EntityType == nameof(Person) && z.EntityId == p.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(p => new { p.Id, p.Name, p.CaseNumber, p.Description })
                    .ToListAsync(cancellationToken);
                // Aliase separat als flache Abfrage über die Kind-Tabelle laden (WHERE PersonId IN …).
                // Bewusst KEIN SelectMany über die Navigation und KEINE Collection-Projektion mit .ToList():
                // beides erzeugt auf MySQL/MariaDB ein nicht übersetzbares CROSS APPLY bzw. LATERAL.
                var ids = raw.Select(x => x.Id).ToList();
                var aliasByPerson = (await db.PersonAliases
                        .Where(a => ids.Contains(a.PersonId))
                        .Select(a => new { a.PersonId, a.AliasName })
                        .ToListAsync(cancellationToken))
                    .GroupBy(a => a.PersonId)
                    .ToDictionary(g => g.Key, g => g.Select(a => a.AliasName).ToList());
                var candidates = raw.Select(x =>
                {
                    var aliases = aliasByPerson.TryGetValue(x.Id, out var list) ? list : new List<string>();
                    return new FuzzyCandidate(x.Id, x.Name, x.CaseNumber, x.Description ?? string.Empty,
                        max
                            ? TextSimilarity.Tokens(new[] { x.Name, x.CaseNumber, x.Description }.Concat(aliases).ToArray())
                            : TextSimilarity.Tokens(new[] { x.Name, x.CaseNumber }.Concat(aliases).ToArray()));
                });
                hit = FuzzySupplement(nameof(Person), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Person), "Personen", hit));
            }
        }

        // ---- Fraktionen (Name/Aktenzeichen/Art/Beschreibung/Ziele; Max zusätzlich Anwesen/Funk/Darkchat/Ausstellungszeiten) ----
        if (Active(nameof(Faction)))
        {
            var q = db.Factions.Where(f => isLeadership || !f.IsClassified);
            if (hasText)
            {
                q = q.Where(f => f.Name.Contains(s!) || f.CaseNumber.Contains(s!)
                    || (f.Kind != null && f.Kind.Contains(s!))
                    || (f.Description != null && f.Description.Contains(s!))
                    || (f.Targets != null && f.Targets.Contains(s!))
                    || (max && (
                           (f.Estate != null && f.Estate.Contains(s!))
                        || (f.Radio != null && f.Radio.Contains(s!))
                        || (f.Darkchat != null && f.Darkchat.Contains(s!))
                        || (f.IssuingTimes != null && f.IssuingTimes.Contains(s!)))));
            }
            if (hasTags)
            {
                q = q.Where(f => db.TagMappings.Any(z => z.EntityType == nameof(Faction) && z.EntityId == f.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(f => f.Name).Take(MaxPerCategory)
                .Select(f => new SearchHit(nameof(Faction), f.Id, f.Name, f.Kind ?? string.Empty, f.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.Factions.Where(f => isLeadership || !f.IsClassified);
                if (hasTags)
                {
                    @base = @base.Where(f => db.TagMappings.Any(z => z.EntityType == nameof(Faction) && z.EntityId == f.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(f => f.ModifiedAt ?? f.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(f => new { f.Id, f.Name, f.CaseNumber, f.Kind, f.Description, f.Targets, f.Estate, f.Radio, f.Darkchat, f.IssuingTimes })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Name, x.CaseNumber, x.Kind ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Name, x.CaseNumber, x.Kind, x.Description, x.Targets, x.Estate, x.Radio, x.Darkchat, x.IssuingTimes)
                        : TextSimilarity.Tokens(x.Name, x.CaseNumber)));
                hit = FuzzySupplement(nameof(Faction), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Faction), "Fraktionen", hit));
            }
        }

        // ---- Personengruppen (Name/Aktenzeichen/Beschreibung/Ziele/Art; Ziele jetzt analog Fraktion/Partei) ----
        if (Active(nameof(PersonGroup)))
        {
            var q = db.PersonGroups.Where(g => isLeadership || !g.IsClassified);
            if (hasText)
            {
                // Auch nach Kategorie-Namen (z. B. „Persönlichkeit", „Person of Interest") suchbar.
                var matchingKinds = GroupsKindDisplay.All
                    .Where(a => GroupsKindDisplay.Name(a).Contains(s!, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                q = q.Where(g => g.Name.Contains(s!) || g.CaseNumber.Contains(s!)
                    || (g.Description != null && g.Description.Contains(s!))
                    || (g.Targets != null && g.Targets.Contains(s!))
                    || matchingKinds.Contains(g.Kind));
            }
            if (hasTags)
            {
                q = q.Where(g => db.TagMappings.Any(z => z.EntityType == nameof(PersonGroup) && z.EntityId == g.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(g => g.Name).Take(MaxPerCategory)
                .Select(g => new SearchHit(nameof(PersonGroup), g.Id, g.Name, g.Description ?? string.Empty, g.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.PersonGroups.Where(g => isLeadership || !g.IsClassified);
                if (hasTags)
                {
                    @base = @base.Where(g => db.TagMappings.Any(z => z.EntityType == nameof(PersonGroup) && z.EntityId == g.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(g => g.ModifiedAt ?? g.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(g => new { g.Id, g.Name, g.CaseNumber, g.Description, g.Targets })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Name, x.CaseNumber, x.Description ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Name, x.CaseNumber, x.Description, x.Targets)
                        : TextSimilarity.Tokens(x.Name, x.CaseNumber)));
                hit = FuzzySupplement(nameof(PersonGroup), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(PersonGroup), "Personengruppen", hit));
            }
        }

        // ---- Parteien (Name/Aktenzeichen/Beschreibung/Ziele/Bemerkungen) ----
        if (Active(nameof(Party)))
        {
            var q = db.Parties.Where(p => isLeadership || !p.IsClassified);
            if (hasText)
            {
                q = q.Where(p => p.Name.Contains(s!) || p.CaseNumber.Contains(s!)
                    || (p.Description != null && p.Description.Contains(s!))
                    || (p.Targets != null && p.Targets.Contains(s!))
                    || (p.Remarks != null && p.Remarks.Contains(s!)));
            }
            if (hasTags)
            {
                q = q.Where(p => db.TagMappings.Any(z => z.EntityType == nameof(Party) && z.EntityId == p.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(p => p.Name).Take(MaxPerCategory)
                .Select(p => new SearchHit(nameof(Party), p.Id, p.Name, p.Description ?? string.Empty, p.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.Parties.Where(p => isLeadership || !p.IsClassified);
                if (hasTags)
                {
                    @base = @base.Where(p => db.TagMappings.Any(z => z.EntityType == nameof(Party) && z.EntityId == p.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(p => new { p.Id, p.Name, p.CaseNumber, p.Description, p.Targets, p.Remarks })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Name, x.CaseNumber, x.Description ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Name, x.CaseNumber, x.Description, x.Targets, x.Remarks)
                        : TextSimilarity.Tokens(x.Name, x.CaseNumber)));
                hit = FuzzySupplement(nameof(Party), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Party), "Parteien", hit));
            }
        }

        // ---- Operationen (Titel/Aktenzeichen/Ablauf/Ergebnis/Ort/Typ/Bemerkungen) ----
        if (Active(nameof(Operation)))
        {
            var q = db.Operations.Where(o => isLeadership || !o.IsClassified);
            if (hasText)
            {
                q = q.Where(o => o.Title.Contains(s!) || o.CaseNumber.Contains(s!)
                    || (o.Expiry != null && o.Expiry.Contains(s!))
                    || (o.Result != null && o.Result.Contains(s!))
                    || (o.Location != null && o.Location.Contains(s!))
                    || (o.Type != null && o.Type.Contains(s!))
                    || (o.Remarks != null && o.Remarks.Contains(s!)));
            }
            if (hasTags)
            {
                q = q.Where(o => db.TagMappings.Any(z => z.EntityType == nameof(Operation) && z.EntityId == o.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(o => o.Title).Take(MaxPerCategory)
                .Select(o => new SearchHit(nameof(Operation), o.Id, o.Title, o.Expiry ?? o.Type ?? string.Empty, o.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.Operations.Where(o => isLeadership || !o.IsClassified);
                if (hasTags)
                {
                    @base = @base.Where(o => db.TagMappings.Any(z => z.EntityType == nameof(Operation) && z.EntityId == o.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(o => o.ModifiedAt ?? o.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(o => new { o.Id, o.Title, o.CaseNumber, o.Type, o.Location, o.Expiry, o.Result, o.Remarks })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Title, x.CaseNumber, x.Expiry ?? x.Type ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Title, x.CaseNumber, x.Type, x.Location, x.Expiry, x.Result, x.Remarks)
                        : TextSimilarity.Tokens(x.Title, x.CaseNumber)));
                hit = FuzzySupplement(nameof(Operation), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Operation), "Operationen", hit));
            }
        }

        // ---- Taskforces (Name/Aktenzeichen/Zweck/Bemerkungen) ----
        if (Active(nameof(Taskforce)))
        {
            var q = db.Taskforces.OnlyVisible(db, isLeadership, meId);
            if (hasText)
            {
                q = q.Where(t => t.Name.Contains(s!) || t.CaseNumber.Contains(s!)
                    || (t.Purpose != null && t.Purpose.Contains(s!))
                    || (t.Remarks != null && t.Remarks.Contains(s!)));
            }
            if (hasTags)
            {
                q = q.Where(t => db.TagMappings.Any(z => z.EntityType == nameof(Taskforce) && z.EntityId == t.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(t => t.Name).Take(MaxPerCategory)
                .Select(t => new SearchHit(nameof(Taskforce), t.Id, t.Name, t.Purpose ?? string.Empty, t.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.Taskforces.OnlyVisible(db, isLeadership, meId);
                if (hasTags)
                {
                    @base = @base.Where(t => db.TagMappings.Any(z => z.EntityType == nameof(Taskforce) && z.EntityId == t.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(t => new { t.Id, t.Name, t.CaseNumber, t.Purpose, t.Remarks })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Name, x.CaseNumber, x.Purpose ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Name, x.CaseNumber, x.Purpose, x.Remarks)
                        : TextSimilarity.Tokens(x.Name, x.CaseNumber)));
                hit = FuzzySupplement(nameof(Taskforce), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Taskforce), "Taskforces", hit));
            }
        }

        // ---- Vorgänge/Fälle (Titel/Aktenzeichen/Typ/Beschreibung/Zusammenfassung/Abschlussvermerk) ----
        if (Active(nameof(Case)))
        {
            var q = db.Cases.Where(v => isLeadership || !v.IsClassified);
            if (hasText)
            {
                q = q.Where(v => v.Title.Contains(s!) || v.CaseNumber.Contains(s!)
                    || (v.Type != null && v.Type.Contains(s!))
                    || (v.Description != null && v.Description.Contains(s!))
                    || (v.Summary != null && v.Summary.Contains(s!))
                    || (v.ClosingNote != null && v.ClosingNote.Contains(s!)));
            }
            if (hasTags)
            {
                q = q.Where(v => db.TagMappings.Any(z => z.EntityType == nameof(Case) && z.EntityId == v.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(v => v.Title).Take(MaxPerCategory)
                .Select(v => new SearchHit(nameof(Case), v.Id, v.Title, v.Description ?? v.Type ?? string.Empty, v.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.Cases.Where(v => isLeadership || !v.IsClassified);
                if (hasTags)
                {
                    @base = @base.Where(v => db.TagMappings.Any(z => z.EntityType == nameof(Case) && z.EntityId == v.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(v => v.ModifiedAt ?? v.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(v => new { v.Id, v.Title, v.CaseNumber, v.Type, v.Description, v.Summary, v.ClosingNote })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Title, x.CaseNumber, x.Description ?? x.Type ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Title, x.CaseNumber, x.Type, x.Description, x.Summary, x.ClosingNote)
                        : TextSimilarity.Tokens(x.Title, x.CaseNumber)));
                hit = FuzzySupplement(nameof(Case), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Case), "Vorgänge", hit));
            }
        }

        // ---- Gesetze (Phase 7: Gesetzbuch/Paragraf/Titel/Text; ohne VS-Konzept und ohne Tag-Filter) ----
        if (Active(nameof(Law)) && !hasTags)
        {
            var q = db.Laws.AsQueryable();
            if (hasText)
            {
                q = q.Where(g => g.Title.Contains(s!) || g.Paragraph.Contains(s!)
                    || g.LawBook.Contains(s!) || g.Text.Contains(s!)
                    || (g.Sentence != null && g.Sentence.Contains(s!)));
            }
            var rawLaws = await q.OrderBy(g => g.LawBook).ThenBy(g => g.Paragraph).Take(MaxPerCategory)
                .Select(g => new { g.Id, g.Paragraph, g.Title, g.LawBook })
                .ToListAsync(cancellationToken);
            if (rawLaws.Count > 0)
            {
                var hit = rawLaws
                    .Select(g => new SearchHit(nameof(Law), g.Id, $"{g.Paragraph} {g.Title}", g.LawBook, g.Paragraph))
                    .ToList();
                groups.Add(new SearchResultGroup(nameof(Law), "Gesetze", hit));
            }
        }

        // ---- Aufgaben (Titel/Aktenzeichen/Beschreibung; eingeschränkte nur für Beteiligte/Aufsicht) ----
        if (Active(nameof(Job)))
        {
            var q = db.Jobs.OnlyVisible(db, isLeadership, meId);
            if (hasText)
            {
                q = q.Where(a => a.Title.Contains(s!) || a.CaseNumber.Contains(s!)
                    || (a.Description != null && a.Description.Contains(s!)));
            }
            if (hasTags)
            {
                q = q.Where(a => db.TagMappings.Any(z => z.EntityType == nameof(Job) && z.EntityId == a.Id && tagIds.Contains(z.TagId)));
            }
            var hit = await q.OrderBy(a => a.Title).Take(MaxPerCategory)
                .Select(a => new SearchHit(nameof(Job), a.Id, a.Title, a.Description ?? string.Empty, a.CaseNumber))
                .ToListAsync(cancellationToken);

            if (FuzzyActive(hit.Count))
            {
                var @base = db.Jobs.OnlyVisible(db, isLeadership, meId);
                if (hasTags)
                {
                    @base = @base.Where(a => db.TagMappings.Any(z => z.EntityType == nameof(Job) && z.EntityId == a.Id && tagIds.Contains(z.TagId)));
                }
                var raw = await @base.OrderByDescending(a => a.ModifiedAt ?? a.CreatedAt).Take(FuzzyCandidatesMax)
                    .Select(a => new { a.Id, a.Title, a.CaseNumber, a.Description })
                    .ToListAsync(cancellationToken);
                var candidates = raw.Select(x => new FuzzyCandidate(x.Id, x.Title, x.CaseNumber, x.Description ?? string.Empty,
                    max
                        ? TextSimilarity.Tokens(x.Title, x.CaseNumber, x.Description)
                        : TextSimilarity.Tokens(x.Title, x.CaseNumber)));
                hit = FuzzySupplement(nameof(Job), hit, searchWords, candidates);
            }

            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Job), "Aufgaben", hit));
            }
        }

        // Die folgenden Kategorien sind Text-Inhalte → nur bei vorhandenem Suchtext. Im Max-Modus immer aktiv.
        // Wichtig: expliziter Join auf db.Personen (NICHT Include über die soft-delete-gefilterte
        // Pflichtnavigation), sonst greift das fragile Query-Filter-/Pflichtnavigations-Zusammenspiel.
        if (hasText && ContentActive(nameof(PersonDoc)))
        {
            var hit = await (
                from d in db.PersonDocs
                where (d.Reason != null && d.Reason.Contains(s!)) || (d.ReceivedInformation != null && d.ReceivedInformation.Contains(s!))
                    || (max && d.Faction != null && d.Faction.Contains(s!))
                join p in db.People on d.PersonId equals p.Id
                where (isLeadership || !p.IsClassified)
                    && (!hasTags || db.TagMappings.Any(z => z.EntityType == nameof(Person) && z.EntityId == p.Id && tagIds.Contains(z.TagId)))
                orderby d.Timestamp descending
                select new SearchHit(nameof(PersonDoc), p.Id, p.Name,
                    (d.Reason ?? d.ReceivedInformation) ?? string.Empty, p.CaseNumber))
                .Take(MaxPerCategory)
                .ToListAsync(cancellationToken);
            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(PersonDoc), "Doks", hit));
            }
        }

        if (hasText && ContentActive(nameof(Source)))
        {
            // Quellen aller Akten-Eltern (Person/Fraktion/Gruppe) durchsuchen; Eltern + Sichtbarkeit/Tags
            // anschließend zentral auflösen, damit der Treffer auf die richtige Akte verlinkt.
            var raw = await db.Sources
                .Where(source => source.Title.Contains(s!) || (source.Description != null && source.Description.Contains(s!)))
                .OrderByDescending(source => source.CreatedAt)
                .Select(source => new RawHit(source.EntityType, source.EntityId, source.Title))
                .Take(MaxPerCategory * 4)
                .ToListAsync(cancellationToken);
            var hit = await RecordParentsHitAsync(db, nameof(Source), raw, isLeadership, meId, hasTags, tagIds, cancellationToken);
            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Source), "Quellen", hit));
            }
        }

        if (hasText && ContentActive(nameof(Comment)))
        {
            var raw = await db.Comments
                .Where(comment => comment.Text.Contains(s!))
                .OrderByDescending(comment => comment.CreatedAt)
                .Select(comment => new RawHit(comment.EntityType, comment.EntityId, comment.Text))
                .Take(MaxPerCategory * 4)
                .ToListAsync(cancellationToken);
            var hit = await RecordParentsHitAsync(db, nameof(Comment), raw, isLeadership, meId, hasTags, tagIds, cancellationToken);
            if (hit.Count > 0)
            {
                groups.Add(new SearchResultGroup(nameof(Comment), "Kommentare", hit));
            }
        }

        return groups;
    }

    public async Task<List<QuickHit>> QuickSearchAsync(string text, bool isLeadership, string? meId, int max = 8, CancellationToken cancellationToken = default)
    {
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            return new List<QuickHit>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var people = await db.People
            .Where(p => (isLeadership || !p.IsClassified) && (p.Name.Contains(s) || p.CaseNumber.Contains(s)))
            .OrderBy(p => p.Name).Take(max)
            .Select(p => new QuickHit(nameof(Person), p.Id, p.Name, p.CaseNumber))
            .ToListAsync(cancellationToken);
        var factions = await db.Factions
            .Where(f => (isLeadership || !f.IsClassified) && (f.Name.Contains(s) || f.CaseNumber.Contains(s)))
            .OrderBy(f => f.Name).Take(max)
            .Select(f => new QuickHit(nameof(Faction), f.Id, f.Name, f.CaseNumber))
            .ToListAsync(cancellationToken);
        var groups = await db.PersonGroups
            .Where(g => (isLeadership || !g.IsClassified) && (g.Name.Contains(s) || g.CaseNumber.Contains(s)))
            .OrderBy(g => g.Name).Take(max)
            .Select(g => new QuickHit(nameof(PersonGroup), g.Id, g.Name, g.CaseNumber))
            .ToListAsync(cancellationToken);
        var parties = await db.Parties
            .Where(p => (isLeadership || !p.IsClassified) && (p.Name.Contains(s) || p.CaseNumber.Contains(s)))
            .OrderBy(p => p.Name).Take(max)
            .Select(p => new QuickHit(nameof(Party), p.Id, p.Name, p.CaseNumber))
            .ToListAsync(cancellationToken);
        var operations = await db.Operations
            .Where(o => (isLeadership || !o.IsClassified) && (o.Title.Contains(s) || o.CaseNumber.Contains(s)))
            .OrderBy(o => o.Title).Take(max)
            .Select(o => new QuickHit(nameof(Operation), o.Id, o.Title, o.CaseNumber))
            .ToListAsync(cancellationToken);
        var taskforces = await db.Taskforces.OnlyVisible(db, isLeadership, meId)
            .Where(t => t.Name.Contains(s) || t.CaseNumber.Contains(s))
            .OrderBy(t => t.Name).Take(max)
            .Select(t => new QuickHit(nameof(Taskforce), t.Id, t.Name, t.CaseNumber))
            .ToListAsync(cancellationToken);
        var cases = await db.Cases
            .Where(v => (isLeadership || !v.IsClassified) && (v.Title.Contains(s) || v.CaseNumber.Contains(s)))
            .OrderBy(v => v.Title).Take(max)
            .Select(v => new QuickHit(nameof(Case), v.Id, v.Title, v.CaseNumber))
            .ToListAsync(cancellationToken);
        var jobs = await db.Jobs.OnlyVisible(db, isLeadership, meId)
            .Where(a => a.Title.Contains(s) || a.CaseNumber.Contains(s))
            .OrderBy(a => a.Title).Take(max)
            .Select(a => new QuickHit(nameof(Job), a.Id, a.Title, a.CaseNumber))
            .ToListAsync(cancellationToken);

        // Immer leicht aktive Tippfehler-Toleranz auf Identifikatoren (Name/Titel/Aktenzeichen). Pro
        // Kategorie nur, wenn der Begriff lang genug ist UND noch Platz frei ist (sonst lohnt der Scan nicht).
        var searchWords = TextSimilarity.Tokens(s);
        if (searchWords.Any(w => w.Length >= TextSimilarity.MinWordLength))
        {
            if (people.Count < max)
            {
                var k = await db.People.Where(p => isLeadership || !p.IsClassified)
                    .OrderBy(p => p.Name).Take(FuzzyCandidatesMax)
                    .Select(p => new { p.Id, p.Name, p.CaseNumber }).ToListAsync(cancellationToken);
                people = QuickFuzzy(nameof(Person), people, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (factions.Count < max)
            {
                var k = await db.Factions.Where(f => isLeadership || !f.IsClassified)
                    .OrderBy(f => f.Name).Take(FuzzyCandidatesMax)
                    .Select(f => new { f.Id, f.Name, f.CaseNumber }).ToListAsync(cancellationToken);
                factions = QuickFuzzy(nameof(Faction), factions, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (groups.Count < max)
            {
                var k = await db.PersonGroups.Where(g => isLeadership || !g.IsClassified)
                    .OrderBy(g => g.Name).Take(FuzzyCandidatesMax)
                    .Select(g => new { g.Id, g.Name, g.CaseNumber }).ToListAsync(cancellationToken);
                groups = QuickFuzzy(nameof(PersonGroup), groups, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (parties.Count < max)
            {
                var k = await db.Parties.Where(p => isLeadership || !p.IsClassified)
                    .OrderBy(p => p.Name).Take(FuzzyCandidatesMax)
                    .Select(p => new { p.Id, p.Name, p.CaseNumber }).ToListAsync(cancellationToken);
                parties = QuickFuzzy(nameof(Party), parties, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (operations.Count < max)
            {
                var k = await db.Operations.Where(o => isLeadership || !o.IsClassified)
                    .OrderBy(o => o.Title).Take(FuzzyCandidatesMax)
                    .Select(o => new { o.Id, Name = o.Title, o.CaseNumber }).ToListAsync(cancellationToken);
                operations = QuickFuzzy(nameof(Operation), operations, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (taskforces.Count < max)
            {
                var k = await db.Taskforces.OnlyVisible(db, isLeadership, meId)
                    .OrderBy(t => t.Name).Take(FuzzyCandidatesMax)
                    .Select(t => new { t.Id, Name = t.Name, t.CaseNumber }).ToListAsync(cancellationToken);
                taskforces = QuickFuzzy(nameof(Taskforce), taskforces, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (cases.Count < max)
            {
                var k = await db.Cases.Where(v => isLeadership || !v.IsClassified)
                    .OrderBy(v => v.Title).Take(FuzzyCandidatesMax)
                    .Select(v => new { v.Id, Name = v.Title, v.CaseNumber }).ToListAsync(cancellationToken);
                cases = QuickFuzzy(nameof(Case), cases, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
            if (jobs.Count < max)
            {
                var k = await db.Jobs.OnlyVisible(db, isLeadership, meId)
                    .OrderBy(a => a.Title).Take(FuzzyCandidatesMax)
                    .Select(a => new { a.Id, Name = a.Title, a.CaseNumber }).ToListAsync(cancellationToken);
                jobs = QuickFuzzy(nameof(Job), jobs, searchWords, k.Select(x => (x.Id, x.Name, x.CaseNumber)), max);
            }
        }

        // Rundlauf-Mischung, damit Personen die Trefferliste nicht verdrängen und alle Kategorien erscheinen.
        return Shuffle(people, factions, groups, parties, operations, taskforces, cases, jobs).Take(max).ToList();
    }

    /// <summary>Mischt mehrere Trefferlisten im Rundlauf (P, F, G, …) für eine faire Verteilung.</summary>
    private static IEnumerable<QuickHit> Shuffle(params List<QuickHit>[] lists)
    {
        for (var index = 0; ; index++)
        {
            var some = false;
            foreach (var list in lists)
            {
                if (index < list.Count)
                {
                    some = true;
                    yield return list[index];
                }
            }
            if (!some)
            {
                yield break;
            }
        }
    }

    /// <summary>Kandidat für den in-memory Fuzzy-Pass: Anzeige-Daten + die zu vergleichenden Wörter.</summary>
    private sealed record FuzzyCandidate(string Id, string Display, string CaseNumber, string Snippet, IReadOnlyList<string> Tokens);

    /// <summary>
    /// Hängt an eine bereits ermittelte Substring-Trefferliste die per Levenshtein ähnlichen Kandidaten
    /// an (dedupliziert gegen die vorhandenen Ziel-Ids, sortiert nach aufsteigender Editierdistanz).
    /// Substring-Treffer bleiben vorne (höhere Relevanz); Gesamtzahl auf <see cref="MaxProKategorie"/> gekappt.
    /// </summary>
    private static List<SearchHit> FuzzySupplement(
        string category, List<SearchHit> substring, IReadOnlyList<string> searchWords, IEnumerable<FuzzyCandidate> candidates)
    {
        if (searchWords.Count == 0)
        {
            return substring;
        }
        var exists = substring.Select(t => t.TargetId).ToHashSet();
        var fuzzy = new List<(SearchHit Hit, int Distance)>();
        foreach (var k in candidates)
        {
            if (exists.Contains(k.Id))
            {
                continue;
            }
            if (TextSimilarity.PhraseSimilar(searchWords, k.Tokens, out var distance))
            {
                fuzzy.Add((new SearchHit(category, k.Id, k.Display, k.Snippet, k.CaseNumber), distance));
            }
        }
        if (fuzzy.Count == 0)
        {
            return substring;
        }
        var result = new List<SearchHit>(substring);
        result.AddRange(fuzzy.OrderBy(f => f.Distance).Select(f => f.Hit));
        return result.Count > MaxPerCategory ? result.Take(MaxPerCategory).ToList() : result;
    }

    /// <summary>Leichtgewichtige Fuzzy-Ergänzung für die Schnellsuche: Identifikatoren (Name/Titel + Aktenzeichen).</summary>
    private static List<QuickHit> QuickFuzzy(
        string category, List<QuickHit> already, IReadOnlyList<string> searchWords,
        IEnumerable<(string Id, string Name, string CaseNumber)> candidates, int max)
    {
        var exists = already.Select(t => t.TargetId).ToHashSet();
        var fuzzy = new List<(QuickHit Hit, int Distance)>();
        foreach (var k in candidates)
        {
            if (exists.Contains(k.Id))
            {
                continue;
            }
            if (TextSimilarity.PhraseSimilar(searchWords, TextSimilarity.Tokens(k.Name, k.CaseNumber), out var distance))
            {
                fuzzy.Add((new QuickHit(category, k.Id, k.Name, k.CaseNumber), distance));
            }
        }
        if (fuzzy.Count == 0)
        {
            return already;
        }
        var result = new List<QuickHit>(already);
        result.AddRange(fuzzy.OrderBy(f => f.Distance).Take(max).Select(f => f.Hit));
        return result;
    }

    /// <summary>Roh-Treffer eines polymorphen Inhalts (Quelle/Kommentar): Eltern-Typ/-Id + Anzeige-Schnipsel.</summary>
    private sealed record RawHit(string EntityType, string EntityId, string Snippet);

    /// <summary>
    /// Löst Roh-Treffer (Quellen/Kommentare) auf ihre Eltern-Akte auf: Name/Aktenzeichen je Typ
    /// (Person/Fraktion/Personengruppe), filtert Verschlusssachen (außer Führung) und – falls gefordert –
    /// nach Tags der Eltern-Akte. Reihenfolge der Roh-Treffer bleibt erhalten; auf <see cref="MaxProKategorie"/> gekürzt.
    /// </summary>
    private static async Task<List<SearchHit>> RecordParentsHitAsync(
        AppDbContext db, string category, List<RawHit> raw, bool isLeadership, string? meId, bool hasTags, List<string> tagIds, CancellationToken cancellationToken)
    {
        if (raw.Count == 0)
        {
            return new();
        }

        var personIds = raw.Where(r => r.EntityType == nameof(Person)).Select(r => r.EntityId).Distinct().ToList();
        var factionIds = raw.Where(r => r.EntityType == nameof(Faction)).Select(r => r.EntityId).Distinct().ToList();
        var groupsIds = raw.Where(r => r.EntityType == nameof(PersonGroup)).Select(r => r.EntityId).Distinct().ToList();
        var partyIds = raw.Where(r => r.EntityType == nameof(Party)).Select(r => r.EntityId).Distinct().ToList();
        var operationIds = raw.Where(r => r.EntityType == nameof(Operation)).Select(r => r.EntityId).Distinct().ToList();
        var taskforceIds = raw.Where(r => r.EntityType == nameof(Taskforce)).Select(r => r.EntityId).Distinct().ToList();
        var caseIds = raw.Where(r => r.EntityType == nameof(Case)).Select(r => r.EntityId).Distinct().ToList();
        var jobIds = raw.Where(r => r.EntityType == nameof(Job)).Select(r => r.EntityId).Distinct().ToList();

        // (Typ, Id) → (Name, Aktenzeichen, Verschlusssache). Gelöschte Akten fehlen (globaler Filter).
        var map = new Dictionary<(string, string), (string Name, string CaseNumber, bool Classified)>();
        foreach (var x in await db.People.Where(p => personIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(Person), x.Id)] = (x.Name, x.CaseNumber, x.IsClassified);
        }
        foreach (var x in await db.Factions.Where(f => factionIds.Contains(f.Id))
                     .Select(f => new { f.Id, f.Name, f.CaseNumber, f.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(Faction), x.Id)] = (x.Name, x.CaseNumber, x.IsClassified);
        }
        foreach (var x in await db.PersonGroups.Where(g => groupsIds.Contains(g.Id))
                     .Select(g => new { g.Id, g.Name, g.CaseNumber, g.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(PersonGroup), x.Id)] = (x.Name, x.CaseNumber, x.IsClassified);
        }
        foreach (var x in await db.Parties.Where(p => partyIds.Contains(p.Id))
                     .Select(p => new { p.Id, p.Name, p.CaseNumber, p.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(Party), x.Id)] = (x.Name, x.CaseNumber, x.IsClassified);
        }
        foreach (var x in await db.Operations.Where(o => operationIds.Contains(o.Id))
                     .Select(o => new { o.Id, o.Title, o.CaseNumber, o.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(Operation), x.Id)] = (x.Title, x.CaseNumber, x.IsClassified);
        }
        foreach (var x in await db.Taskforces.Where(t => taskforceIds.Contains(t.Id))
                     .Select(t => new { t.Id, t.Name, t.CaseNumber, t.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(Taskforce), x.Id)] = (x.Name, x.CaseNumber, x.IsClassified);
        }
        foreach (var x in await db.Cases.Where(v => caseIds.Contains(v.Id))
                     .Select(v => new { v.Id, v.Title, v.CaseNumber, v.IsClassified }).ToListAsync(cancellationToken))
        {
            map[(nameof(Case), x.Id)] = (x.Title, x.CaseNumber, x.IsClassified);
        }
        // Eingeschränkte Aufgaben nur für Beteiligte/Aufsicht in die Map aufnehmen – sonst werden Treffer auf
        // Kommentaren/Quellen einer eingeschränkten Aufgabe unten (fehlt in der Map → continue) ausgeblendet.
        foreach (var x in await db.Jobs.OnlyVisible(db, isLeadership, meId).Where(a => jobIds.Contains(a.Id))
                     .Select(a => new { a.Id, a.Title, a.CaseNumber }).ToListAsync(cancellationToken))
        {
            map[(nameof(Job), x.Id)] = (x.Title, x.CaseNumber, false);
        }

        // Tag-Filter: welche Eltern-Akten tragen mindestens einen der gewählten Tags?
        HashSet<(string, string)>? withTag = null;
        if (hasTags)
        {
            withTag = (await db.TagMappings
                .Where(z => tagIds.Contains(z.TagId)
                    && ((z.EntityType == nameof(Person) && personIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(Faction) && factionIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(PersonGroup) && groupsIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(Party) && partyIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(Operation) && operationIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(Taskforce) && taskforceIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(Case) && caseIds.Contains(z.EntityId))
                     || (z.EntityType == nameof(Job) && jobIds.Contains(z.EntityId))))
                .Select(z => new { z.EntityType, z.EntityId }).ToListAsync(cancellationToken))
                .Select(z => (z.EntityType, z.EntityId)).ToHashSet();
        }

        var result = new List<SearchHit>();
        foreach (var r in raw)
        {
            if (!map.TryGetValue((r.EntityType, r.EntityId), out var info))
            {
                continue; // Eltern-Akte gelöscht/unbekannt → ausblenden.
            }
            if (info.Classified && !isLeadership)
            {
                continue;
            }
            if (hasTags && (withTag is null || !withTag.Contains((r.EntityType, r.EntityId))))
            {
                continue;
            }
            result.Add(new SearchHit(category, r.EntityId, info.Name, r.Snippet, info.CaseNumber, r.EntityType));
            if (result.Count >= MaxPerCategory)
            {
                break;
            }
        }
        return result;
    }
}
