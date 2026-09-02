using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicReportService" />
public class PublicReportService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IMemoryCache cache) : IPublicReportService
{
    private const string CacheKey = "OeffentlicheLageberichte";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    private const string NotFound = "Dieser Lagebericht existiert nicht mehr.";

    public async Task<PublicReportSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        // the module switch is checked outside the content cache: caching "module is off" as an empty snapshot would
        // keep the pages dark for a whole cache window after someone turns the module back on
        if (!await modules.IsEnabledAsync(PublicModules.SituationReports, cancellationToken))
        {
            return PublicReportSnapshot.Empty;
        }
        return await LoadAsync(cancellationToken);
    }

    public async Task<PublicReportView?> GetByPeriodAsync(string? period, CancellationToken cancellationToken = default)
    {
        // parsed before it is looked up, so the route has exactly one address per month: the strict form is the only
        // one that resolves, and anything else reads as "not found" rather than as a second spelling of the same page
        if (!ReportPeriod.TryParse(period, out var year, out var month))
        {
            return null;
        }
        // its own row read, NOT a lookup in the hub snapshot: that list is capped at HubLimit (two years), so the
        // 25th monthly report would retire month one from an address the docs call citable
        if (!await modules.IsEnabledAsync(PublicModules.SituationReports, cancellationToken))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheLageberichte
            .AsNoTracking()
            .Where(r => r.Status == PublicReportStatus.Veroeffentlicht && r.Year == year && r.Month == month)
            .Select(r => new PublicReportView(r.Year, r.Month, r.ContentTitle ?? string.Empty,
                r.ContentHtml ?? string.Empty, r.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicReportEdit>> GetAllAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // projected rather than Include'd: only the codename is wanted, and pulling the whole identity user would
        // carry the publisher's clear name into a panel the read-only supervision renders
        return await db.OeffentlicheLageberichte
            .AsNoTracking()
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new PublicReportEdit(
                r.Id,
                r.Year,
                r.Month,
                r.Title,
                r.Status,
                (r.DraftHtml ?? string.Empty) != (r.ContentHtml ?? string.Empty)
                    || r.Title != (r.ContentTitle ?? string.Empty),
                r.PublishedAt,
                r.PublishedBy!.Codename,
                r.SituationReportId,
                // both navigations are optional, so both are LEFT joined: a report whose anchor was deleted stays in
                // this list instead of vanishing from it while a count that touches no navigation keeps counting it
                r.SituationReport != null,
                r.ModifiedAt ?? r.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicReportAnchor>> GetAnchorsAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        // read straight off the table rather than through ISituationReportService, and not for convenience: the public
        // pages inject this service, so taking that dependency would build the whole internal statistics stack
        // (statistics, funding statistics, notifications) into an anonymous visitor's object graph. Four columns are
        // all a picker needs, and GetArchiveAsync additionally resolves a codename per report for nobody's benefit.
        // NewForAnchorAsync reads the same table the same way, so the two also stay consistent.
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SituationReports
            .AsNoTracking()
            // a month whose public text was deleted is free again, so only living rows count as taken
            .Where(l => !db.OeffentlicheLageberichte.Any(r => r.SituationReportId == l.Id))
            .OrderByDescending(l => l.Year).ThenByDescending(l => l.Month)
            .Select(l => new PublicReportAnchor(l.Id, l.Year, l.Month, l.Title))
            .ToListAsync(cancellationToken);
    }

    public async Task<PublicReportDraft?> GetDraftAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheLageberichte
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new PublicReportDraft(r.Title, r.DraftHtml ?? string.Empty))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> SaveDraftAsync(PublicReportInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicReportWrite(actor);

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            throw new InvalidOperationException("Der Lagebericht braucht einen Titel.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        OeffentlicherLagebericht row;
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            row = await NewForAnchorAsync(db, input.SituationReportId, cancellationToken);
            db.OeffentlicheLageberichte.Add(row);
        }
        else
        {
            var id = input.Id;
            // anchor and period are immutable after creation: they are the public address, and an edit must not move it
            row = await db.OeffentlicheLageberichte.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);
        }

        row.Title = Cut(title, ReportRules.MaxTitle);
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
        Permission.RequirePublicReportWrite(actor);
        await modules.RequireEnabledAsync(PublicModules.SituationReports, cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheLageberichte.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);

        // clean again rather than trust the stored draft: publishing is the moment the HTML becomes reachable anonymously
        var html = HtmlCleanup.Clean(row.DraftHtml);
        // empty means neither text nor picture; a report that is only a chart is content, and PlainText alone would
        // have rejected it as an empty draft
        if (HtmlCleanup.PlainText(html).Length == 0 && !html.Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ein leerer Entwurf lässt sich nicht veröffentlichen.");
        }

        row.DraftHtml = html;
        row.ContentHtml = html;
        row.ContentTitle = row.Title;
        row.Status = PublicReportStatus.Veroeffentlicht;
        // stamped once, same reason as a press release: the hub shows this date, so fixing a typo in a report from
        // March must not claim it was issued today. Retracting clears it, so one that goes out again is dated anew
        row.PublishedAt ??= DateTime.UtcNow;
        row.PublishedById ??= actor.GetAgentId();

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicReportWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheLageberichte.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);

        // no module gate, here or in Delete: publishing needs a live module, depublishing never — otherwise the kill
        // switch would make retracting impossible, exactly the wrong way round.
        // ContentHtml and the period stay: visibility hangs on Status alone, so going back online is one click on the
        // same address. The publication date does not: it says since when this stands outside
        row.Status = PublicReportStatus.Entwurf;
        row.PublishedAt = null;
        row.PublishedById = null;

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicReportWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheLageberichte.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException(NotFound);

        if (row.Status == PublicReportStatus.Veroeffentlicht)
        {
            // otherwise deleting would be a silent depublication with no reason on the record
            throw new InvalidOperationException("Zuerst zurückziehen, dann löschen.");
        }

        db.OeffentlicheLageberichte.Remove(row);
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task<List<OeffentlicherLagebericht>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheLageberichte
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.IsDeleted)
            .OrderByDescending(r => r.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicReportWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheLageberichte
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Dieser Lagebericht liegt nicht im Papierkorb.");

        // the trash is the second door to "one living report per month": after a delete somebody may have written a
        // new text for the same month, and restoring across that would put two of them on one address
        var year = row.Year;
        var month = row.Month;
        if (await db.OeffentlicheLageberichte.AnyAsync(r => r.Year == year && r.Month == month, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Für {ReportPeriod.Label(year, month)} gibt es bereits einen Lagebericht.");
        }

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        // a restore comes back as a draft: nothing goes public again as a side effect of undoing a delete
        row.Status = PublicReportStatus.Entwurf;

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    /// <summary>Builds a row for an archived month, taking year and month from the anchor rather than the caller.</summary>
    private static async Task<OeffentlicherLagebericht> NewForAnchorAsync(AppDbContext db, string? anchorId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            throw new InvalidOperationException("Ein Lagebericht braucht einen Monatsbericht als Anker.");
        }

        var anchor = await db.SituationReports
            .AsNoTracking()
            .Where(l => l.Id == anchorId)
            .Select(l => new { l.Id, l.Year, l.Month })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Dieser Monatsbericht existiert nicht mehr.");

        // one living public report per month; no unique index, because with soft delete that would block the month
        // forever — the rule lives here, over the living rows
        if (await db.OeffentlicheLageberichte
                .AnyAsync(r => r.Year == anchor.Year && r.Month == anchor.Month, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Für {ReportPeriod.Label(anchor.Year, anchor.Month)} gibt es bereits einen Lagebericht.");
        }

        return new OeffentlicherLagebericht
        {
            SituationReportId = anchor.Id,
            Year = anchor.Year,
            Month = anchor.Month,
        };
    }

    private async Task<PublicReportSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PublicReportSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        PublicReportSnapshot snapshot;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // the anchor is never dereferenced: title and period are snapshot fields on the row, so a deleted monthly
            // report cannot take a published text off the air and there is nothing here to suppress
            var rows = await db.OeffentlicheLageberichte
                .AsNoTracking()
                .Where(r => r.Status == PublicReportStatus.Veroeffentlicht)
                .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
                .Take(ReportRules.HubLimit)
                .Select(r => new PublicReportView(r.Year, r.Month, r.ContentTitle ?? string.Empty,
                    r.ContentHtml ?? string.Empty, r.PublishedAt))
                .ToListAsync(cancellationToken);

            snapshot = new PublicReportSnapshot(
                Cards: rows.Select(r => new PublicReportCard(r.Year, r.Month, r.Title, r.PublishedAt)).ToList(),
                // GroupBy rather than ToDictionary: the month is unique among living rows by service rule, not by an
                // index, and a snapshot that throws would hide every report instead of the one disputed month
                ByPeriod: rows.GroupBy(r => ReportPeriod.Format(r.Year, r.Month), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase),
                // stripped once per cache fill, not once per anonymous search request
                SearchText: rows.GroupBy(r => ReportPeriod.Format(r.Year, r.Month), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => HtmlCleanup.PlainText(g.First().Html),
                        StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            // an unreachable database shows no reports rather than a stack trace to an anonymous visitor
            return PublicReportSnapshot.Empty;
        }

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    /// <summary>The one save path of this table: nothing writes it without dropping the snapshot.</summary>
    /// <remarks>A file scan holds this shape (<c>PublicReportCacheDisciplineTests</c>).</remarks>
    private async Task SaveAndInvalidateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
