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

        var changed = false;
        for (var i = 0; i < chapters.Count; i++)
        {
            var shipped = chapters[i];
            if (!existing.TryGetValue(shipped.Key, out var row))
            {
                db.HandbuchKapitel.Add(new HandbookChapter
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
                });
                changed = true;
                continue;
            }

            if (row.IsCustomised || row.IsDeleted || row.SeedRevision >= revision)
            {
                continue;
            }

            row.Slug = shipped.Slug;
            row.Title = shipped.Title;
            row.Description = shipped.Description;
            row.IconName = shipped.Icon;
            row.SortOrder = i;
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
                    written.Add((row, shipped));
                    changed = true;
                    continue;
                }

                if (row.IsCustomised || row.IsDeleted || row.SeedRevision >= revision)
                {
                    continue;
                }

                row.ChapterId = chapterRow.Id;
                row.Slug = shipped.Slug;
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

        var changed = false;
        foreach (var shipped in terms)
        {
            var articleId = shipped.ArticleKey is { } key && articleIdByKey.TryGetValue(key, out var id) ? id : null;

            if (!existing.TryGetValue(shipped.Key, out var row))
            {
                db.HandbuchBegriffe.Add(new GlossaryTerm
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
                });
                changed = true;
                continue;
            }

            if (row.IsCustomised || row.SeedRevision >= revision)
            {
                continue;
            }

            row.Term = shipped.Term;
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
