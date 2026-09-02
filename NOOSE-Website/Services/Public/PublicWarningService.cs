using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicWarningService" />
public class PublicWarningService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IMemoryCache cache) : IPublicWarningService
{
    private const string CacheKey = "OeffentlicheWarnungen";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    private const string NotFound = "Diese Warnung existiert nicht mehr.";

    public async Task<PublicWarningSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        // the module switch is checked outside the content cache: caching "module is off" as an empty snapshot would
        // keep the page dark for a whole cache window after someone turns the module back on
        if (!await modules.IsEnabledAsync(PublicModules.Warnings, cancellationToken))
        {
            return PublicWarningSnapshot.Empty;
        }
        return await LoadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WarningEdit>> GetAllAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // projected rather than Include'd: only the codename is wanted, and pulling the whole identity user would
        // carry the publisher's clear name into a panel the read-only supervision renders
        return await db.OeffentlicheWarnungen
            .AsNoTracking()
            .OrderByDescending(w => w.PublishedAt ?? w.CreatedAt)
            .Select(w => new WarningEdit(
                w.Id,
                w.Title,
                w.Status,
                // length as well as equality: the SQL comparison runs under a case- and accent-insensitive server
                // collation, so a capital letter or an umlaut alone read as "nothing to publish". The body stays
                // in SQL because WarningEdit deliberately carries no HTML. Residual gap, knowingly: an edit that
                // changes only case or accents AND keeps the exact same length.
                (w.DraftHtml ?? string.Empty) != (w.ContentHtml ?? string.Empty)
                    || (w.DraftHtml ?? string.Empty).Length != (w.ContentHtml ?? string.Empty).Length
                    || w.Title != (w.ContentTitle ?? string.Empty)
                    || w.Title.Length != (w.ContentTitle ?? string.Empty).Length,
                w.ValidUntil,
                w.ValidUntil != null && w.ValidUntil <= now,
                w.PublishedAt,
                w.PublishedBy!.Codename,
                w.ModifiedAt ?? w.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarningDraft?> GetDraftAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheWarnungen
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new WarningDraft(w.Title, w.DraftHtml ?? string.Empty, w.ValidUntil))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> SaveDraftAsync(WarningInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireWarningWrite(actor);

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            throw new InvalidOperationException("Die Warnung braucht einen Titel.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        OeffentlicheWarnung row;
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            row = new OeffentlicheWarnung();
            db.OeffentlicheWarnungen.Add(row);
        }
        else
        {
            var id = input.Id;
            row = await db.OeffentlicheWarnungen.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);
        }

        row.Title = Cut(title, WarningRules.MaxTitle);
        // read live and stored once, unlike the title and the body: extending a warning is not a new statement, and
        // demanding a re-publication would let one expire because nobody pressed the second button
        row.ValidUntil = PublicExpiry.From(input.ValidUntil);
        // null leaves the draft alone, an empty string clears it. Without that split a caller that only changes the
        // title would wipe the body, and the loss would be silent
        if (input.DraftHtml is not null)
        {
            row.DraftHtml = HtmlCleanup.Clean(input.DraftHtml);
        }

        await SaveAndInvalidateAsync(db, cancellationToken);
        return row.Id;
    }

    public async Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWarningWrite(actor);
        await modules.RequireEnabledAsync(PublicModules.Warnings, cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheWarnungen.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);

        // the read path filters on the same field, so publishing an expired warning would report success and change
        // nothing anybody can see
        if (row.ValidUntil is { } until && until <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Die Gültigkeit liegt in der Vergangenheit; erst verlängern, dann veröffentlichen.");
        }

        // clean again rather than trust the stored draft: publishing is the moment the HTML becomes reachable anonymously
        var html = HtmlCleanup.Clean(row.DraftHtml);
        // empty means neither text nor picture; a warning that is only an image is content, and PlainText alone
        // would have rejected it as an empty draft
        if (HtmlCleanup.PlainText(html).Length == 0 && !html.Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ein leerer Entwurf lässt sich nicht veröffentlichen.");
        }

        row.DraftHtml = html;
        row.ContentHtml = html;
        row.ContentTitle = row.Title;
        row.Status = PublicWarningStatus.Veroeffentlicht;
        // stamped once, same reason as a press release: the hub shows and sorts by this date, so fixing a typo in a
        // week-old warning must not claim it was issued today. Retracting clears it, so a warning that comes back
        // after the danger returned is dated anew
        row.PublishedAt ??= DateTime.UtcNow;
        row.PublishedById ??= actor.GetAgentId();

        // no Discord push, unlike a press release: a warning expires, and the channel post would still claim danger
        // after it did. The same argument that keeps PublicWantedExpired off the routable list
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWarningWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheWarnungen.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);

        // no module gate, here or in Delete: publishing needs a live module, depublishing never — otherwise the kill
        // switch would make retracting impossible, exactly the wrong way round.
        // ContentHtml stays: visibility hangs on Status alone, so going back online is one click. The publication
        // date does not: it says since when this stands outside, and after a retraction the answer is "it does not"
        row.Status = PublicWarningStatus.Entwurf;
        row.PublishedAt = null;
        row.PublishedById = null;

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWarningWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheWarnungen.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);

        if (row.Status == PublicWarningStatus.Veroeffentlicht)
        {
            // otherwise deleting would be a silent depublication with no reason on the record
            throw new InvalidOperationException("Zuerst zurückziehen, dann löschen.");
        }

        db.OeffentlicheWarnungen.Remove(row);
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task<List<OeffentlicheWarnung>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheWarnungen
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.IsDeleted)
            .OrderByDescending(w => w.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWarningWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheWarnungen
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == id && w.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Diese Warnung liegt nicht im Papierkorb.");

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        // a restore comes back as a draft: nothing goes public again as a side effect of undoing a delete
        row.Status = PublicWarningStatus.Entwurf;

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    private async Task<PublicWarningSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PublicWarningSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        PublicWarningSnapshot snapshot;
        try
        {
            // the filter is the control, not a worker: an expired warning falls out of the read path on its own, so
            // there is nothing to sweep and nothing that leaks when a sweep does not run. It may stand for up to one
            // cache window past its expiry, which an expiry measured in days does not care about
            var now = DateTime.UtcNow;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.OeffentlicheWarnungen
                .AsNoTracking()
                .Where(w => w.Status == PublicWarningStatus.Veroeffentlicht
                    && (w.ValidUntil == null || w.ValidUntil > now))
                .OrderByDescending(w => w.PublishedAt)
                .Take(WarningRules.HubLimit)
                .Select(w => new PublicWarningCard(w.ContentTitle ?? string.Empty, w.ContentHtml ?? string.Empty,
                    w.ValidUntil, w.PublishedAt))
                .ToListAsync(cancellationToken);

            // stripped once per cache fill, not once per anonymous search request
            snapshot = new PublicWarningSnapshot(rows, rows.Select(w => HtmlCleanup.PlainText(w.Html)).ToList());
        }
        catch (Exception)
        {
            // an unreachable database shows no warnings rather than a stack trace to an anonymous visitor
            return PublicWarningSnapshot.Empty;
        }

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    /// <summary>The one save path of this table: nothing writes it without dropping the snapshot.</summary>
    /// <remarks>A file scan holds this shape (<c>PublicWarningCacheDisciplineTests</c>).</remarks>
    private async Task SaveAndInvalidateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
