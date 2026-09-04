using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MudBlazor;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicPageService" />
public class PublicPageService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IMemoryCache cache) : IPublicPageService
{
    private const string CacheKey = "OeffentlicheSeiten";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    public async Task<PublicPageSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        // the module switch is checked outside the content cache: caching "module is off" as an empty snapshot would
        // keep the pages dark for a whole cache window after someone turns the module back on
        if (!await modules.IsEnabledAsync(PublicModules.InfoPages, cancellationToken))
        {
            return PublicPageSnapshot.Empty;
        }
        return await LoadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicPageLink>> GetMenuAsync(CancellationToken cancellationToken = default)
        => (await GetPublishedAsync(cancellationToken)).Menu;

    public async Task<PublicPageView?> GetAsync(string slug, CancellationToken cancellationToken = default)
        => (await GetPublishedAsync(cancellationToken)).Find(slug);

    public async Task<PublicPageView?> GetPreviewAsync(string slug, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        // stored slugs are lowercase, and the public path matches case-insensitively; do the same here so a preview
        // link does not depend on the database collation
        var wanted = (slug ?? string.Empty).ToLowerInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheSeiten
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == wanted, cancellationToken);
        if (row is null)
        {
            return null;
        }

        // deliberately not gated on the module: preparing content while the module is still off is the point of a draft
        return new PublicPageView(row.Slug, row.Title, row.DraftHtml ?? string.Empty, row.PublishedAt, IsDraft: true);
    }

    public async Task<IReadOnlyList<PublicPageEdit>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // projected rather than Include'd: only the codename is wanted, and pulling the whole identity user would
        // carry the publisher's clear name into a panel the read-only supervision renders
        return await db.OeffentlicheSeiten
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Title)
            .Select(p => new PublicPageEdit(
                p.Id,
                p.Slug,
                p.Title,
                p.MenuTitle,
                p.IconName,
                p.SortOrder,
                p.Status,
                p.ShowInMenu,
                // length as well as equality: the SQL comparison runs under a case- and accent-insensitive
                // server collation, so a capital letter or an umlaut alone read as "nothing to publish". The body
                // stays in SQL because PublicPageEdit deliberately carries no HTML. Residual gap, knowingly: an
                // edit that changes only case or accents AND keeps the exact same length.
                (p.DraftHtml ?? string.Empty) != (p.ContentHtml ?? string.Empty)
                    || (p.DraftHtml ?? string.Empty).Length != (p.ContentHtml ?? string.Empty).Length,
                p.PublishedAt,
                p.PublishedBy!.Codename,
                p.ModifiedAt ?? p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetDraftAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheSeiten
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => p.DraftHtml ?? string.Empty)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> SaveDraftAsync(PublicPageInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            throw new InvalidOperationException("Die Seite braucht einen Titel.");
        }

        // forgiving on the way in, strict on what gets stored: a German title folds to a slug, and whatever is left
        // still has to satisfy the route shape
        var slug = PublicPageSlug.Normalize(input.Slug);
        if (!PublicPageSlug.IsValid(slug))
        {
            throw new InvalidOperationException(
                "Die Adresse muss aus Kleinbuchstaben, Zahlen und einzelnen Bindestrichen bestehen.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // the slug is unique among live rows only; a soft-deleted page keeps its own, and a DB-level unique index
        // would block reusing the address after a delete. The two branches are spelled out because a null id in
        // "Id != input.Id" translates to SQL NULL, which would silently match nothing
        var id = input.Id;
        var taken = string.IsNullOrWhiteSpace(id)
            ? await db.OeffentlicheSeiten.AnyAsync(p => p.Slug == slug, cancellationToken)
            : await db.OeffentlicheSeiten.AnyAsync(p => p.Slug == slug && p.Id != id, cancellationToken);
        if (taken)
        {
            throw new InvalidOperationException($"Die Adresse „/info/{slug}“ ist schon belegt.");
        }

        OeffentlicheSeite row;
        if (string.IsNullOrWhiteSpace(id))
        {
            row = new OeffentlicheSeite();
            db.OeffentlicheSeiten.Add(row);
        }
        else
        {
            row = await db.OeffentlicheSeiten.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Seite existiert nicht mehr.");

            // Two authors on one page: the second save would overwrite the first one's body, and the only copy
            // left would be a diff in the audit log. Refuse instead - the same expression the panel row carries.
            if (input.LoadedModifiedAt is { } loaded && (row.ModifiedAt ?? row.CreatedAt) != loaded)
            {
                throw new InvalidOperationException("Diese Seite wurde in der Zwischenzeit von jemand anderem "
                    + "gespeichert. Schließe den Editor, lade die Liste neu und übertrage deine Änderungen.");
            }
        }

        // The read path serves this very column, so moving it here would relocate a live page's public address
        // without a publish click - every external link would die while the editor promises the opposite.
        // Retracting keeps the content, so retract -> change address -> publish is a two-click round trip.
        if (row.Status == PublicPageStatus.Veroeffentlicht
            && !string.Equals(row.Slug, slug, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Die Seite ist unter „/info/{row.Slug}“ veröffentlicht. Zieh sie "
                + "zuerst zurück, dann lässt sich die Adresse ändern.");
        }

        row.Slug = slug;
        row.Title = Cut(title, PublicPageRules.MaxTitle);
        row.MenuTitle = CutOrNull(Empty(input.MenuTitle), PublicPageRules.MaxMenuTitle);
        // an unknown icon name is dropped, not stored: MudBlazor renders an icon value as markup
        row.IconName = PublicModules.IsKnownIcon(input.IconName) ? input.IconName!.Trim() : null;
        row.SortOrder = Math.Clamp(input.SortOrder, 0, 9999);
        row.ShowInMenu = input.ShowInMenu;
        // null leaves the draft alone, an empty string clears it. Without that split a caller that only changes the
        // title would wipe the body, and the loss would be silent
        if (input.DraftHtml is not null)
        {
            row.DraftHtml = HtmlCleanup.Clean(input.DraftHtml);
        }

        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
        return row.Id;
    }

    public async Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheSeiten.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Seite existiert nicht mehr.");

        // clean again rather than trust the stored draft: publishing is the moment the HTML becomes reachable anonymously
        var html = HtmlCleanup.Clean(row.DraftHtml);
        // empty means neither text nor picture; a page that is only an image is content, and PlainText alone
        // would have rejected it as an empty draft
        if (HtmlCleanup.PlainText(html).Length == 0 && !html.Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ein leerer Entwurf lässt sich nicht veröffentlichen.");
        }

        row.DraftHtml = html;
        row.ContentHtml = html;
        row.Status = PublicPageStatus.Veroeffentlicht;
        row.PublishedAt = DateTime.UtcNow;
        row.PublishedById = actor.GetAgentId();

        // the audit interceptor stamps the row itself, and the Status change reads as the action; no manual row needed
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheSeiten.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Seite existiert nicht mehr.");

        // ContentHtml stays as the last published copy; visibility hangs on Status, and keeping it makes going back
        // online a one-click affair without touching a newer draft
        row.Status = PublicPageStatus.Entwurf;

        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheSeiten.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Seite existiert nicht mehr.");

        db.OeffentlicheSeiten.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    public async Task<List<OeffentlicheSeite>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheSeiten
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheSeiten
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Diese Seite liegt nicht im Papierkorb.");

        // a deleted page keeps its slug and the address may have been reused since; restoring on top of that would
        // leave two live pages on one address, and an ambiguous /info costs every page, not just this one
        var slug = row.Slug;
        if (await db.OeffentlicheSeiten.AnyAsync(p => p.Slug == slug, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Die Adresse „/info/{slug}“ ist inzwischen belegt. Ändere sie an der anderen Seite, dann lässt sich diese wiederherstellen.");
        }

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        // a restore comes back as a draft: nothing goes public again as a side effect of undoing a delete
        row.Status = PublicPageStatus.Entwurf;

        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    private async Task<PublicPageSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PublicPageSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        PublicPageSnapshot snapshot;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // the FAQ row is held out of every Information read: it answers on /faq under a module of its own, so
            // leaving it here would list it in the menu, serve the same text a second time under /info/faq and
            // offer the outside search two hits for one page
            var rows = await db.OeffentlicheSeiten
                .AsNoTracking()
                .Where(p => p.Status == PublicPageStatus.Veroeffentlicht && p.Slug != PublicFaq.PageSlug)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Title)
                .ToListAsync(cancellationToken);

            // one page per address. The slug carries no unique index (a deleted page keeps its own), so a duplicate
            // that got in some other way must cost at most the ambiguous page — building the dictionary straight
            // from the rows would throw and the catch below would blank every published page instead.
            var unique = rows
                .GroupBy(p => p.Slug, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            snapshot = new PublicPageSnapshot(
                Menu: unique.Where(p => p.ShowInMenu)
                    .Select(p => new PublicPageLink(
                        p.Slug,
                        string.IsNullOrWhiteSpace(p.MenuTitle) ? p.Title : p.MenuTitle!,
                        PublicModules.IconFor(p.IconName, PublicModules.PageDefaultIcon),
                        p.SortOrder))
                    .ToList(),
                Pages: unique.ToDictionary(
                    p => p.Slug,
                    p => new PublicPageView(p.Slug, p.Title, p.ContentHtml ?? string.Empty, p.PublishedAt),
                    StringComparer.OrdinalIgnoreCase),
                // stripped once per cache fill, not once per anonymous search request
                SearchText: unique.ToDictionary(
                    p => p.Slug,
                    p => HtmlCleanup.PlainText(p.ContentHtml),
                    StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            // an unreachable database shows no editorial pages rather than a stack trace to an anonymous visitor
            return PublicPageSnapshot.Empty;
        }

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    private static string? CutOrNull(string? value, int max) => value is null ? null : Cut(value, max);
}
