using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Handbook;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Handbook;

/// <summary>Writes the shipped handbook into the database, idempotently, without ever overwriting an edit.</summary>
/// <remarks>
/// The same four rules as the changelog seeder: an untouched row is kept current with the shipped text, an edited row
/// is never written again, a deleted row is not revived, and a row that was never shipped is invisible here.
/// <para>
/// Steps are the exception to row-by-row handling: they are replaced wholesale with their article, because a
/// walkthrough is one thing and keying each card separately would let a renumbering leave orphans behind.
/// </para>
/// <para>
/// The shipped HTML is run through the sanitizer on the way in. It is authored here and therefore trusted, but
/// cleaning it means the database can never hold markup the reader path would have to cope with - and a tag that
/// does not survive the filter shows up at once instead of on somebody's screen.
/// </para>
/// </remarks>
public static class HandbookSeeder
{
    public static Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
        => SeedAsync(db, HandbookContent.Chapters, HandbookContent.Terms, HandbookContent.Revision, cancellationToken);

    /// <summary>The same pass over explicit lists; the tests drive the revision rule through this overload.</summary>
    public static async Task SeedAsync(
        AppDbContext db,
        IReadOnlyList<HandbookContent.SeededChapter> chapters,
        IReadOnlyList<HandbookContent.SeededTerm> terms,
        int revision,
        CancellationToken cancellationToken = default)
    {
        await SeedChaptersAsync(db, chapters, revision, cancellationToken);
        var chapterIdByKey = await db.HandbuchKapitel.IgnoreQueryFilters()
            .Where(c => c.SeedKey != null)
            .Select(c => new { c.SeedKey, c.Id, c.IsDeleted })
            .ToDictionaryAsync(c => c.SeedKey!, c => (c.Id, c.IsDeleted), StringComparer.Ordinal, cancellationToken);

        var articleIdByKey = await SeedArticlesAsync(db, chapters, revision, chapterIdByKey, cancellationToken);
        await SeedTermsAsync(db, terms, revision, articleIdByKey, cancellationToken);
    }

    private static async Task SeedChaptersAsync(
        AppDbContext db,
        IReadOnlyList<HandbookContent.SeededChapter> chapters,
        int revision,
        CancellationToken cancellationToken)
    {
        // deleted rows count as known: re-creating a chapter somebody threw away would be a surprise, not a repair
        var existing = await db.HandbuchKapitel.IgnoreQueryFilters()
            .Where(c => c.SeedKey != null)
            .ToDictionaryAsync(c => c.SeedKey!, StringComparer.Ordinal, cancellationToken);
        var owners = await SlugOwnersAsync(db, cancellationToken);

        var changed = false;
        for (var i = 0; i < chapters.Count; i++)
        {
            var shipped = chapters[i];
            if (!existing.TryGetValue(shipped.Key, out var row))
            {
                // an editor's own chapter owns the slug without carrying a seed key, so it is invisible above
                if (owners.ContainsKey(shipped.Slug))
                {
                    continue;
                }
                var fresh = new HandbookChapter
                {
                    Slug = shipped.Slug,
                    Title = shipped.Title,
                    Description = shipped.Description,
                    IconName = shipped.Icon,
                    SortOrder = i,
                    IsVisible = true,
                    SeedKey = shipped.Key,
                    SeedRevision = revision,
                    IsCustomised = false,
                };
                db.HandbuchKapitel.Add(fresh);
                owners[fresh.Slug] = fresh.Id;
                changed = true;
                continue;
            }

            if (row.IsCustomised || row.IsDeleted)
            {
                continue;
            }

            // structure ahead of the revision guard, same reason as the articles: a chapter inserted in the
            // middle would otherwise share its sort order with the one it displaced
            if (row.SortOrder != i)
            {
                row.SortOrder = i;
                changed = true;
            }

            if (row.SeedRevision >= revision)
            {
                continue;
            }

            // a rename onto a slug somebody else holds would throw just like an insert would
            if (Free(owners, shipped.Slug, row.Id))
            {
                owners.Remove(row.Slug);
                row.Slug = shipped.Slug;
                owners[shipped.Slug] = row.Id;
            }
            row.Title = shipped.Title;
            row.Description = shipped.Description;
            row.IconName = shipped.Icon;
            row.SeedRevision = revision;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Returns article id per seed key, so the glossary can link to the article it belongs to.</summary>
    private static async Task<Dictionary<string, string>> SeedArticlesAsync(
        AppDbContext db,
        IReadOnlyList<HandbookContent.SeededChapter> chapters,
        int revision,
        IReadOnlyDictionary<string, (string Id, bool IsDeleted)> chapterIdByKey,
        CancellationToken cancellationToken)
    {
        var existing = await db.HandbuchArtikel.IgnoreQueryFilters()
            .Where(a => a.SeedKey != null)
            .ToDictionaryAsync(a => a.SeedKey!, StringComparer.Ordinal, cancellationToken);
        var owners = Owners(await db.HandbuchArtikel.IgnoreQueryFilters()
            .Select(a => new SlugRow(a.Id, a.Slug))
            .ToListAsync(cancellationToken));

        var written = new List<(HandbookArticle Row, HandbookContent.SeededArticle Shipped)>();
        var changed = false;

        foreach (var chapter in chapters)
        {
            if (!chapterIdByKey.TryGetValue(chapter.Key, out var chapterRow) || chapterRow.IsDeleted)
            {
                // a chapter in the bin takes its articles with it rather than stranding them
                continue;
            }

            for (var i = 0; i < chapter.Articles.Length; i++)
            {
                var shipped = chapter.Articles[i];
                var sortOrder = i * 10;

                if (!existing.TryGetValue(shipped.Key, out var row))
                {
                    // an editor's own article owns the slug without carrying a seed key, so it is invisible above
                    if (owners.ContainsKey(shipped.Slug))
                    {
                        continue;
                    }
                    row = new HandbookArticle
                    {
                        ChapterId = chapterRow.Id,
                        Slug = shipped.Slug,
                        Title = shipped.Title,
                        Summary = shipped.Summary,
                        ContentHtml = HtmlCleanup.Clean(shipped.ContentHtml),
                        RoleplayHtml = Html(shipped.RoleplayHtml),
                        DiagramKey = shipped.DiagramKey,
                        NavKey = shipped.NavKey,
                        SortOrder = sortOrder,
                        IsVisible = true,
                        SeedKey = shipped.Key,
                        SeedRevision = revision,
                        IsCustomised = false,
                    };
                    db.HandbuchArtikel.Add(row);
                    owners[row.Slug] = row.Id;
                    written.Add((row, shipped));
                    changed = true;
                    continue;
                }

                if (row.IsCustomised || row.IsDeleted)
                {
                    continue;
                }

                // Position and chapter follow the shipped book even without a revision bump, because they are
                // structure rather than text: a new article inserted in the middle takes its neighbour's sort
                // order, and leaving the neighbour behind left two rows on the same number with the order
                // between them undefined. Only an untouched row is moved - an editor who re-sorted keeps theirs.
                if (row.ChapterId != chapterRow.Id || row.SortOrder != sortOrder)
                {
                    row.ChapterId = chapterRow.Id;
                    row.SortOrder = sortOrder;
                    changed = true;
                }

                if (row.SeedRevision >= revision)
                {
                    continue;
                }

                // a rename onto a slug somebody else holds would throw just like an insert would
                if (Free(owners, shipped.Slug, row.Id))
                {
                    owners.Remove(row.Slug);
                    row.Slug = shipped.Slug;
                    owners[shipped.Slug] = row.Id;
                }
                row.Title = shipped.Title;
                row.Summary = shipped.Summary;
                row.ContentHtml = HtmlCleanup.Clean(shipped.ContentHtml);
                row.RoleplayHtml = Html(shipped.RoleplayHtml);
                row.DiagramKey = shipped.DiagramKey;
                row.NavKey = shipped.NavKey;
                row.SortOrder = sortOrder;
                row.SeedRevision = revision;
                written.Add((row, shipped));
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await SeedStepsAsync(db, written, cancellationToken);

        return await db.HandbuchArtikel.IgnoreQueryFilters()
            .Where(a => a.SeedKey != null)
            .Select(a => new { a.SeedKey, a.Id })
            .ToDictionaryAsync(a => a.SeedKey!, a => a.Id, StringComparer.Ordinal, cancellationToken);
    }

    /// <summary>Steps follow their article wholesale: drop what is there, write what ships.</summary>
    private static async Task SeedStepsAsync(
        AppDbContext db,
        IReadOnlyList<(HandbookArticle Row, HandbookContent.SeededArticle Shipped)> written,
        CancellationToken cancellationToken)
    {
        if (written.Count == 0)
        {
            return;
        }

        var ids = written.Select(w => w.Row.Id).ToList();
        var old = await db.HandbuchSchritte.Where(s => ids.Contains(s.ArticleId)).ToListAsync(cancellationToken);
        if (old.Count > 0)
        {
            db.HandbuchSchritte.RemoveRange(old);
        }

        foreach (var (row, shipped) in written)
        {
            if (shipped.Steps is not { Length: > 0 } steps)
            {
                continue;
            }
            for (var i = 0; i < steps.Length; i++)
            {
                db.HandbuchSchritte.Add(new HandbookStep
                {
                    ArticleId = row.Id,
                    Number = i + 1,
                    IconName = steps[i].Icon,
                    Title = steps[i].Title,
                    Text = steps[i].Text,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTermsAsync(
        AppDbContext db,
        IReadOnlyList<HandbookContent.SeededTerm> terms,
        int revision,
        IReadOnlyDictionary<string, string> articleIdByKey,
        CancellationToken cancellationToken)
    {
        var existing = await db.HandbuchBegriffe
            .Where(t => t.SeedKey != null)
            .ToDictionaryAsync(t => t.SeedKey!, StringComparer.Ordinal, cancellationToken);
        var owners = Owners(await db.HandbuchBegriffe
            .Select(t => new SlugRow(t.Id, t.Term))
            .ToListAsync(cancellationToken));

        var changed = false;
        foreach (var shipped in terms)
        {
            var articleId = shipped.ArticleKey is { } key && articleIdByKey.TryGetValue(key, out var id) ? id : null;

            if (!existing.TryGetValue(shipped.Key, out var row))
            {
                // an editor's own term owns the name without carrying a seed key, so it is invisible above
                if (owners.ContainsKey(shipped.Term))
                {
                    continue;
                }
                var fresh = new GlossaryTerm
                {
                    Term = shipped.Term,
                    Synonyms = shipped.Synonyms,
                    ShortDefinition = shipped.ShortDefinition,
                    ExplanationHtml = Html(shipped.ExplanationHtml),
                    ArticleId = articleId,
                    IsVisible = true,
                    SeedKey = shipped.Key,
                    SeedRevision = revision,
                    IsCustomised = false,
                };
                db.HandbuchBegriffe.Add(fresh);
                owners[fresh.Term] = fresh.Id;
                changed = true;
                continue;
            }

            if (row.IsCustomised || row.SeedRevision >= revision)
            {
                continue;
            }

            // a rename onto a name somebody else holds would throw just like an insert would
            if (Free(owners, shipped.Term, row.Id))
            {
                owners.Remove(row.Term);
                row.Term = shipped.Term;
                owners[shipped.Term] = row.Id;
            }
            row.Synonyms = shipped.Synonyms;
            row.ShortDefinition = shipped.ShortDefinition;
            row.ExplanationHtml = Html(shipped.ExplanationHtml);
            row.ArticleId = articleId;
            row.SeedRevision = revision;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>One row of a uniquely indexed name column, for the collision guard.</summary>
    private sealed record SlugRow(string Id, string Name);

    /// <summary>Who currently owns which unique name; the guard against a startup-killing insert.</summary>
    /// <remarks>
    /// Slug and glossary term carry a unique index, and a hand-written row holds one without carrying a seed key -
    /// so the dictionaries above, which are keyed on the seed key, cannot see it. Writing onto such a name throws
    /// inside the startup seeding block and takes the whole application down, restart loop included. Case-insensitive
    /// because MySQL compares these columns that way; the in-memory test database does not, and the stricter of the
    /// two is the safe one to assume.
    /// </remarks>
    private static Dictionary<string, string> Owners(IEnumerable<SlugRow> rows)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            map.TryAdd(row.Name, row.Id);
        }
        return map;
    }

    private static async Task<Dictionary<string, string>> SlugOwnersAsync(AppDbContext db, CancellationToken ct)
        => Owners(await db.HandbuchKapitel.IgnoreQueryFilters()
            .Select(c => new SlugRow(c.Id, c.Slug))
            .ToListAsync(ct));

    /// <summary>Whether this row may take that name - free, or already its own.</summary>
    private static bool Free(IReadOnlyDictionary<string, string> owners, string name, string rowId)
        => !owners.TryGetValue(name, out var owner) || string.Equals(owner, rowId, StringComparison.Ordinal);

    /// <summary>Filtered HTML, or null when nothing survives - an empty string would render an empty box.</summary>
    private static string? Html(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }
        var cleaned = HtmlCleanup.Clean(html);
        return HtmlCleanup.PlainText(cleaned).Length == 0 ? null : cleaned;
    }
}
