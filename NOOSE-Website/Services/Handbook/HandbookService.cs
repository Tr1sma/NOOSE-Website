using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Handbook;
using NOOSE_Website.Models.Handbook;

namespace NOOSE_Website.Services.Handbook;

/// <inheritdoc cref="IHandbookService" />
public sealed class HandbookService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache) : IHandbookService
{
    // The help button asks for an article on every page header, and the header sits on 36 pages - with
    // prerendering that is four round trips per view. The book changes a few times a month, so it is cached.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private const string GenerationKey = "handbuch:gen";

    // Keys carry the generation, so one increment retires every cached entry at once. Cheaper and less
    // error-prone than tracking a key per nav entry, and an editor sees their change immediately.
    private long Generation => cache.TryGetValue(GenerationKey, out long g) ? g : 0;

    // NeverRemove: were the counter dropped while entries keyed on an older generation survived, the key
    // would roll back to 0 and serve them again - a stale article with no way to notice
    private void Evict() => cache.Set(GenerationKey, Generation + 1,
        new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    /// <summary>Boxed so a "there is no article" answer is cacheable too; null alone is indistinguishable from a miss.</summary>
    private sealed record CachedCard(HandbookArticleCard? Card);

    public async Task<List<HandbookChapterView>> GetChaptersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var chapters = await db.HandbuchKapitel.AsNoTracking()
            .Where(c => c.IsVisible)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Title)
            .ToListAsync(cancellationToken);
        if (chapters.Count == 0)
        {
            return [];
        }

        // flat WHERE IN, not a collection projection: Pomelo translates no lateral join on MySQL
        var ids = chapters.Select(c => c.Id).ToList();
        var articles = await db.HandbuchArtikel.AsNoTracking()
            .Where(a => ids.Contains(a.ChapterId) && a.IsVisible)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Title)
            .ToListAsync(cancellationToken);
        var byChapter = articles.GroupBy(a => a.ChapterId).ToDictionary(g => g.Key, g => g.ToList());

        return chapters.Select(c => new HandbookChapterView(
            c.Id, c.Slug, c.Title, c.Description, c.IconName,
            (byChapter.TryGetValue(c.Id, out var list) ? list : [])
                .Select(a => new HandbookArticleCard(a.Id, a.Slug, a.Title, a.Summary, c.Slug, c.Title))
                .ToList()))
            .ToList();
    }

    public async Task<HandbookArticleView?> GetArticleAsync(string slug, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var article = await db.HandbuchArtikel.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug && a.IsVisible, cancellationToken);
        if (article is null)
        {
            return null;
        }

        var chapter = await db.HandbuchKapitel.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == article.ChapterId, cancellationToken);
        if (chapter is null || !chapter.IsVisible)
        {
            // an article in a hidden chapter is hidden too; the chapter is how a reader reaches it
            return null;
        }

        var steps = await db.HandbuchSchritte.AsNoTracking()
            .Where(s => s.ArticleId == article.Id)
            .OrderBy(s => s.Number)
            .ToListAsync(cancellationToken);

        return new HandbookArticleView(
            article.Id, article.Slug, article.Title, article.Summary,
            article.ContentHtml, article.RoleplayHtml, article.DiagramKey,
            chapter.Slug, chapter.Title,
            steps.Select(s => new HandbookStepView(s.Number, s.IconName, s.Title, s.Text)).ToList());
    }

    public async Task<HandbookArticleCard?> GetArticleForNavKeyAsync(string navKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(navKey))
        {
            return null;
        }

        var key = $"handbuch:nav:{Generation}:{navKey}";
        if (cache.TryGetValue(key, out CachedCard? hit) && hit is not null)
        {
            return hit.Card;
        }

        var card = await LoadArticleForNavKeyAsync(navKey, cancellationToken);
        cache.Set(key, new CachedCard(card), CacheDuration);
        return card;
    }

    private async Task<HandbookArticleCard?> LoadArticleForNavKeyAsync(string navKey, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.HandbuchArtikel.AsNoTracking()
            .Where(a => a.NavKey == navKey && a.IsVisible)
            .OrderBy(a => a.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (article is null)
        {
            return null;
        }

        var chapter = await db.HandbuchKapitel.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == article.ChapterId, cancellationToken);
        if (chapter is null || !chapter.IsVisible)
        {
            return null;
        }

        return new HandbookArticleCard(article.Id, article.Slug, article.Title, article.Summary,
            chapter.Slug, chapter.Title);
    }

    public async Task<List<GlossaryTermView>> GetGlossaryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var terms = await db.HandbuchBegriffe.AsNoTracking()
            .Where(t => t.IsVisible)
            .OrderBy(t => t.Term)
            .ToListAsync(cancellationToken);
        if (terms.Count == 0)
        {
            return [];
        }

        var articleIds = terms.Where(t => t.ArticleId != null).Select(t => t.ArticleId!).Distinct().ToList();
        var articles = articleIds.Count == 0
            ? []
            : await db.HandbuchArtikel.AsNoTracking()
                .Where(a => articleIds.Contains(a.Id) && a.IsVisible)
                .Select(a => new { a.Id, a.Slug, a.Title })
                .ToListAsync(cancellationToken);
        var byId = articles.ToDictionary(a => a.Id);

        return terms.Select(t =>
        {
            var link = t.ArticleId is not null && byId.TryGetValue(t.ArticleId, out var a) ? a : null;
            return new GlossaryTermView(t.Id, t.Term, t.Synonyms, t.ShortDefinition, t.ExplanationHtml,
                link?.Slug, link?.Title);
        }).ToList();
    }

    // The TASK is cached, not the result: every rich-text block on a page asks at the same moment, and
    // caching only the finished matcher lets all of them miss and run the query. A failed load is dropped
    // again so one hiccup does not switch the bubbles off for the whole cache lifetime.
    public Task<GlossaryMatcher> GetGlossaryMatcherAsync(CancellationToken cancellationToken = default)
    {
        var key = $"handbuch:glossar:{Generation}";
        if (cache.TryGetValue(key, out Task<GlossaryMatcher>? hit) && hit is not null)
        {
            return hit;
        }

        var load = LoadMatcherAsync(key, cancellationToken);
        cache.Set(key, load, CacheDuration);
        return load;
    }

    private async Task<GlossaryMatcher> LoadMatcherAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return GlossaryMatcher.Build(await GetGlossaryAsync(cancellationToken));
        }
        catch
        {
            cache.Remove(key);
            throw;
        }
    }

    public async Task<List<HandbookChapter>> GetAllChaptersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.HandbuchKapitel.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<HandbookArticle>> GetAllArticlesAsync(string chapterId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.HandbuchArtikel.AsNoTracking()
            .Where(a => a.ChapterId == chapterId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Title)
            .ToListAsync(cancellationToken);
    }

    // --- chapters ---------------------------------------------------------

    public async Task<HandbookChapter> CreateChapterAsync(HandbookChapterInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);
        var slug = Slug(input.Slug) ?? throw new InvalidOperationException("Das Kapitel braucht eine Adresse (Slug).");
        var title = Clean(input.Title, 160) ?? throw new InvalidOperationException("Das Kapitel braucht einen Titel.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await RequireFreeChapterSlugAsync(db, slug, null, cancellationToken);

        var chapter = new HandbookChapter
        {
            Slug = slug,
            Title = title,
            Description = Clean(input.Description, 400),
            IconName = Clean(input.IconName, 64),
            SortOrder = input.SortOrder,
            IsVisible = input.IsVisible,
            IsCustomised = true,
        };
        db.HandbuchKapitel.Add(chapter);
        await db.SaveChangesAsync(cancellationToken);
        Evict();
        return chapter;
    }

    public async Task RefreshChapterAsync(string id, HandbookChapterInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);
        var slug = Slug(input.Slug) ?? throw new InvalidOperationException("Das Kapitel braucht eine Adresse (Slug).");
        var title = Clean(input.Title, 160) ?? throw new InvalidOperationException("Das Kapitel braucht einen Titel.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var chapter = await db.HandbuchKapitel.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Das Kapitel wurde nicht gefunden.");
        await RequireFreeChapterSlugAsync(db, slug, id, cancellationToken);

        chapter.Slug = slug;
        chapter.Title = title;
        chapter.Description = Clean(input.Description, 400);
        chapter.IconName = Clean(input.IconName, 64);
        chapter.SortOrder = input.SortOrder;
        chapter.IsVisible = input.IsVisible;
        // the promise of the seeder: once edited here, the shipped content leaves this row alone for good
        chapter.IsCustomised = true;
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    public async Task DeleteChapterAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var chapter = await db.HandbuchKapitel.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (chapter is null)
        {
            return;
        }
        if (await db.HandbuchArtikel.AnyAsync(a => a.ChapterId == id, cancellationToken))
        {
            throw new InvalidOperationException(
                "Das Kapitel enthält noch Artikel. Verschiebe oder lösche sie zuerst.");
        }
        db.HandbuchKapitel.Remove(chapter); // soft delete via interceptor
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    // --- articles ---------------------------------------------------------

    public async Task<HandbookArticle> CreateArticleAsync(HandbookArticleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);
        var slug = Slug(input.Slug) ?? throw new InvalidOperationException("Der Artikel braucht eine Adresse (Slug).");
        var title = Clean(input.Title, 200) ?? throw new InvalidOperationException("Der Artikel braucht einen Titel.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.HandbuchKapitel.AnyAsync(c => c.Id == input.ChapterId, cancellationToken))
        {
            throw new InvalidOperationException("Das gewählte Kapitel wurde nicht gefunden.");
        }
        await RequireFreeArticleSlugAsync(db, slug, null, cancellationToken);

        var article = new HandbookArticle
        {
            ChapterId = input.ChapterId,
            Slug = slug,
            Title = title,
            Summary = Clean(input.Summary, 400),
            ContentHtml = CleanHtml(input.ContentHtml),
            RoleplayHtml = CleanHtml(input.RoleplayHtml),
            DiagramKey = Clean(input.DiagramKey, 64),
            NavKey = Clean(input.NavKey, 64),
            SortOrder = input.SortOrder,
            IsVisible = input.IsVisible,
            IsCustomised = true,
        };
        db.HandbuchArtikel.Add(article);
        await db.SaveChangesAsync(cancellationToken);
        Evict();
        return article;
    }

    public async Task RefreshArticleAsync(string id, HandbookArticleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);
        var slug = Slug(input.Slug) ?? throw new InvalidOperationException("Der Artikel braucht eine Adresse (Slug).");
        var title = Clean(input.Title, 200) ?? throw new InvalidOperationException("Der Artikel braucht einen Titel.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.HandbuchArtikel.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Der Artikel wurde nicht gefunden.");
        if (!await db.HandbuchKapitel.AnyAsync(c => c.Id == input.ChapterId, cancellationToken))
        {
            throw new InvalidOperationException("Das gewählte Kapitel wurde nicht gefunden.");
        }
        await RequireFreeArticleSlugAsync(db, slug, id, cancellationToken);

        article.ChapterId = input.ChapterId;
        article.Slug = slug;
        article.Title = title;
        article.Summary = Clean(input.Summary, 400);
        article.ContentHtml = CleanHtml(input.ContentHtml);
        article.RoleplayHtml = CleanHtml(input.RoleplayHtml);
        article.DiagramKey = Clean(input.DiagramKey, 64);
        article.NavKey = Clean(input.NavKey, 64);
        article.SortOrder = input.SortOrder;
        article.IsVisible = input.IsVisible;
        article.IsCustomised = true;
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    public async Task DeleteArticleAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.HandbuchArtikel.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return;
        }
        db.HandbuchArtikel.Remove(article); // soft delete via interceptor
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    // --- glossary ---------------------------------------------------------

    public async Task<GlossaryTerm> CreateTermAsync(GlossaryTermInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);
        var term = Clean(input.Term, 120) ?? throw new InvalidOperationException("Der Begriff braucht einen Namen.");
        var shortDef = Clean(input.ShortDefinition, 400)
            ?? throw new InvalidOperationException("Der Begriff braucht eine kurze Erklärung.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.HandbuchBegriffe.AnyAsync(t => t.Term == term, cancellationToken))
        {
            throw new InvalidOperationException($"Den Begriff „{term}\" gibt es bereits.");
        }

        var row = new GlossaryTerm
        {
            Term = term,
            Synonyms = Clean(input.Synonyms, 400),
            ShortDefinition = shortDef,
            ExplanationHtml = CleanHtml(input.ExplanationHtml),
            ArticleId = string.IsNullOrWhiteSpace(input.ArticleId) ? null : input.ArticleId,
            IsVisible = input.IsVisible,
            IsCustomised = true,
        };
        db.HandbuchBegriffe.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        Evict();
        return row;
    }

    public async Task RefreshTermAsync(string id, GlossaryTermInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);
        var term = Clean(input.Term, 120) ?? throw new InvalidOperationException("Der Begriff braucht einen Namen.");
        var shortDef = Clean(input.ShortDefinition, 400)
            ?? throw new InvalidOperationException("Der Begriff braucht eine kurze Erklärung.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.HandbuchBegriffe.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Der Begriff wurde nicht gefunden.");
        if (await db.HandbuchBegriffe.AnyAsync(t => t.Id != id && t.Term == term, cancellationToken))
        {
            throw new InvalidOperationException($"Den Begriff „{term}\" gibt es bereits.");
        }

        row.Term = term;
        row.Synonyms = Clean(input.Synonyms, 400);
        row.ShortDefinition = shortDef;
        row.ExplanationHtml = CleanHtml(input.ExplanationHtml);
        row.ArticleId = string.IsNullOrWhiteSpace(input.ArticleId) ? null : input.ArticleId;
        row.IsVisible = input.IsVisible;
        row.IsCustomised = true;
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    // --- trash ------------------------------------------------------------

    public async Task<List<HandbookChapter>> GetChapterTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.HandbuchKapitel.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.IsDeleted)
            .OrderByDescending(c => c.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<HandbookArticle>> GetArticleTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.HandbuchArtikel.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.IsDeleted)
            .OrderByDescending(a => a.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreChapterAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var chapter = await db.HandbuchKapitel.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted, cancellationToken);
        if (chapter is null)
        {
            return;
        }
        await RequireFreeChapterSlugAsync(db, chapter.Slug, id, cancellationToken);
        chapter.IsDeleted = false;
        chapter.DeletedAt = null;
        chapter.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    public async Task RestoreArticleAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadershipWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var article = await db.HandbuchArtikel.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id && a.IsDeleted, cancellationToken);
        if (article is null)
        {
            return;
        }
        await RequireFreeArticleSlugAsync(db, article.Slug, id, cancellationToken);
        // a chapter that went to the bin with it would leave the article unreachable
        if (!await db.HandbuchKapitel.AnyAsync(c => c.Id == article.ChapterId, cancellationToken))
        {
            throw new InvalidOperationException(
                "Das Kapitel des Artikels liegt im Papierkorb. Stelle zuerst das Kapitel wieder her.");
        }
        article.IsDeleted = false;
        article.DeletedAt = null;
        article.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
        Evict();
    }

    // --- helpers ----------------------------------------------------------

    /// <summary>The slug is an address, and a soft-deleted row still owns the unique index.</summary>
    private static async Task RequireFreeChapterSlugAsync(AppDbContext db, string slug, string? exceptId, CancellationToken ct)
    {
        var taken = await db.HandbuchKapitel.IgnoreQueryFilters()
            .AnyAsync(c => c.Slug == slug && (exceptId == null || c.Id != exceptId), ct);
        if (taken)
        {
            throw new InvalidOperationException(
                $"Die Adresse „{slug}\" ist bereits vergeben - möglicherweise von einem Kapitel im Papierkorb.");
        }
    }

    private static async Task RequireFreeArticleSlugAsync(AppDbContext db, string slug, string? exceptId, CancellationToken ct)
    {
        var taken = await db.HandbuchArtikel.IgnoreQueryFilters()
            .AnyAsync(a => a.Slug == slug && (exceptId == null || a.Id != exceptId), ct);
        if (taken)
        {
            throw new InvalidOperationException(
                $"Die Adresse „{slug}\" ist bereits vergeben - möglicherweise von einem Artikel im Papierkorb.");
        }
    }

    /// <summary>Validated on write rather than escaped on read, because it is a URL segment.</summary>
    /// <remarks>
    /// Umlauts are transliterated rather than kept: they are letters, so they would survive the filter below and then
    /// spend the rest of their life percent-encoded in every link, bookmark and Discord message.
    /// </remarks>
    private static string? Slug(string? value)
    {
        var trimmed = value?.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        var chars = trimmed
            .Select(c => (c >= 'a' && c <= 'z') || char.IsDigit(c) || c == '-' ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        return string.IsNullOrEmpty(slug) ? null : (slug.Length > 120 ? slug[..120] : slug);
    }

    /// <summary>Filtered HTML, or null when nothing survives - an empty string would render an empty box.</summary>
    private static string? CleanHtml(string? html)
    {
        var cleaned = HtmlCleanup.Clean(html);
        // an image-only body has no text but is still content
        return HtmlCleanup.PlainText(cleaned).Length == 0
               && !cleaned.Contains("<img", StringComparison.OrdinalIgnoreCase)
            ? null
            : cleaned;
    }

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
