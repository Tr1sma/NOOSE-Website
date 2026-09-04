using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicFaqService" />
public class PublicFaqService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IMemoryCache cache) : IPublicFaqService
{
    private const string CacheKey = "OeffentlichesFaq";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    public async Task<PublicFaqSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        // The module switch is checked outside the content cache: caching "module is off" as an empty snapshot
        // would keep the FAQ dark for a whole cache window after someone turns the module back on. The second
        // gate - the editorial row being published - sits inside LoadAsync, where the context is already open.
        if (!await modules.IsEnabledAsync(PublicModules.Faq, cancellationToken))
        {
            return PublicFaqSnapshot.Empty;
        }
        return await LoadAsync(cancellationToken);
    }

    public async Task<PublicFaqSnapshot> GetPreviewAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        // deliberately past both gates: looking at a question before switching it visible is the point of a preview
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var head = await db.OeffentlicheSeiten
            .AsNoTracking()
            .Where(p => p.Slug == PublicFaq.PageSlug)
            .Select(p => new PublicFaqHead(p.Title, p.DraftHtml ?? string.Empty, p.PublishedAt, true))
            .FirstOrDefaultAsync(cancellationToken);
        return await BuildAsync(db, head, visibleOnly: false, cancellationToken);
    }

    public async Task<PublicFaqAdminView> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rubriken = await db.OeffentlicheFaqRubriken
            .AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ToListAsync(cancellationToken);

        // flat, then grouped in memory: Pomelo translates no LATERAL, so a collection projection would fan out
        // into one query per section
        var entries = await db.OeffentlicheFaqEintraege
            .AsNoTracking()
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Question)
            .Select(e => new { e.RubrikId, Row = new PublicFaqEntryRow(
                e.Id,
                e.Question,
                e.Anchor,
                e.SortOrder,
                e.IsVisible,
                e.AnswerHtml != null && e.AnswerHtml != "") })
            .ToListAsync(cancellationToken);
        var byRubrik = entries
            .GroupBy(e => e.RubrikId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Row).ToList(), StringComparer.Ordinal);

        // read straight from the table rather than through the page service: that one is module-gated, so a
        // switched-off module would report the page as unpublished and send the editor to the wrong switch
        var pageLive = await db.OeffentlicheSeiten
            .AnyAsync(p => p.Slug == PublicFaq.PageSlug && p.Status == PublicPageStatus.Veroeffentlicht,
                cancellationToken);
        var moduleOn = await modules.IsEnabledAsync(PublicModules.Faq, cancellationToken);

        return new PublicFaqAdminView(pageLive, moduleOn, rubriken
            .Select(r => new PublicFaqRubrikRow(
                r.Id,
                r.Title,
                r.Description,
                r.IconName,
                r.SortOrder,
                r.IsVisible,
                r.DefaultOpen,
                byRubrik.TryGetValue(r.Id, out var own) ? own : []))
            .ToList());
    }

    public async Task<string?> GetAnswerAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheFaqEintraege
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.AnswerHtml ?? string.Empty)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> SaveRubrikAsync(PublicFaqRubrikInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            throw new InvalidOperationException("Die Rubrik braucht einen Titel.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        OeffentlicheFaqRubrik row;
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            if (await db.OeffentlicheFaqRubriken.CountAsync(cancellationToken) >= PublicFaqRules.MaxRubriken)
            {
                throw new InvalidOperationException($"Mehr als {PublicFaqRules.MaxRubriken} Rubriken werden "
                    + "unübersichtlich. Fasse zuerst welche zusammen.");
            }
            var last = await db.OeffentlicheFaqRubriken.MaxAsync(r => (int?)r.SortOrder, cancellationToken) ?? 0;
            row = new OeffentlicheFaqRubrik { SortOrder = Math.Min(9999, last + 10) };
            db.OeffentlicheFaqRubriken.Add(row);
        }
        else
        {
            row = await db.OeffentlicheFaqRubriken.FirstOrDefaultAsync(r => r.Id == input.Id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Rubrik existiert nicht mehr.");
        }

        row.Title = Cut(title, PublicFaqRules.MaxTitle);
        row.Description = CutOrNull(Empty(input.Description), PublicFaqRules.MaxDescription);
        // an unknown icon name is dropped, not stored: MudBlazor renders an icon value as markup
        row.IconName = PublicModules.IsKnownIcon(input.IconName) ? input.IconName!.Trim() : null;
        row.IsVisible = input.IsVisible;
        row.DefaultOpen = input.DefaultOpen;

        await CommitAsync(db, cancellationToken);
        return row.Id;
    }

    public async Task<string> SaveEntryAsync(PublicFaqEntryInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        var question = (input.Question ?? string.Empty).Trim();
        if (question.Length == 0)
        {
            throw new InvalidOperationException("Die Frage darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rubrikId = input.RubrikId ?? string.Empty;
        if (!await db.OeffentlicheFaqRubriken.AnyAsync(r => r.Id == rubrikId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Rubrik existiert nicht mehr.");
        }

        OeffentlicheFaqEintrag row;
        if (string.IsNullOrWhiteSpace(input.Id))
        {
            var siblings = await db.OeffentlicheFaqEintraege
                .CountAsync(e => e.RubrikId == rubrikId, cancellationToken);
            if (siblings >= PublicFaqRules.MaxEntriesPerRubrik)
            {
                throw new InvalidOperationException($"Eine Rubrik trägt höchstens "
                    + $"{PublicFaqRules.MaxEntriesPerRubrik} Fragen. Teile sie auf.");
            }
            row = new OeffentlicheFaqEintrag
            {
                SortOrder = await NextEntryOrderAsync(db, rubrikId, cancellationToken),
                // minted once and then left alone: an anchor that followed the wording would break every link
                // somebody already shared the moment a typo is fixed
                Anchor = await MintAnchorAsync(db, question, cancellationToken),
            };
            db.OeffentlicheFaqEintraege.Add(row);
        }
        else
        {
            row = await db.OeffentlicheFaqEintraege.FirstOrDefaultAsync(e => e.Id == input.Id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Frage existiert nicht mehr.");
            // moved to another section: it starts at the end there rather than on top of a stranger's number
            if (!string.Equals(row.RubrikId, rubrikId, StringComparison.Ordinal))
            {
                row.SortOrder = await NextEntryOrderAsync(db, rubrikId, cancellationToken);
            }
        }

        row.RubrikId = rubrikId;
        row.Question = Cut(question, PublicFaqRules.MaxQuestion);
        row.IsVisible = input.IsVisible;
        // null leaves the stored answer alone, an empty string clears it. Without that split a caller that only
        // renames a question would wipe the answer, and the loss would be silent
        if (input.AnswerHtml is not null)
        {
            row.AnswerHtml = HtmlCleanup.Clean(input.AnswerHtml);
        }

        await CommitAsync(db, cancellationToken);
        return row.Id;
    }

    public async Task SetRubrikVisibleAsync(string id, bool visible, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFaqRubriken.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Rubrik existiert nicht mehr.");
        row.IsVisible = visible;
        await CommitAsync(db, cancellationToken);
    }

    public async Task SetEntryVisibleAsync(string id, bool visible, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFaqEintraege.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Frage existiert nicht mehr.");
        row.IsVisible = visible;
        await CommitAsync(db, cancellationToken);
    }

    public async Task MoveRubrikAsync(string id, int delta, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ordered = await db.OeffentlicheFaqRubriken
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ToListAsync(cancellationToken);
        if (!Reorder(ordered, id, delta, r => r.Id, (r, value) => r.SortOrder = value))
        {
            return;
        }
        await CommitAsync(db, cancellationToken);
    }

    public async Task MoveEntryAsync(string id, int delta, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rubrikId = await db.OeffentlicheFaqEintraege
            .Where(e => e.Id == id)
            .Select(e => e.RubrikId)
            .FirstOrDefaultAsync(cancellationToken);
        if (rubrikId is null)
        {
            return;
        }

        // inside its own section only: a question never leapfrogs into the next heading by pressing an arrow
        var ordered = await db.OeffentlicheFaqEintraege
            .Where(e => e.RubrikId == rubrikId)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Question)
            .ToListAsync(cancellationToken);
        if (!Reorder(ordered, id, delta, e => e.Id, (e, value) => e.SortOrder = value))
        {
            return;
        }
        await CommitAsync(db, cancellationToken);
    }

    public async Task DeleteRubrikAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFaqRubriken.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Rubrik existiert nicht mehr.");

        // refused rather than cascaded: these rows are hard-deleted, so taking the questions along would destroy
        // several written answers on one click and no trash would hold them
        if (await db.OeffentlicheFaqEintraege.AnyAsync(e => e.RubrikId == id, cancellationToken))
        {
            throw new InvalidOperationException("Diese Rubrik trägt noch Fragen. Verschiebe sie zuerst in eine "
                + "andere Rubrik oder lösche sie einzeln — gelöschte Antworten liegen in keinem Papierkorb.");
        }

        db.OeffentlicheFaqRubriken.Remove(row);
        await CommitAsync(db, cancellationToken);
    }

    public async Task DeleteEntryAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicPageWrite(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFaqEintraege.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
                  ?? throw new InvalidOperationException("Diese Frage existiert nicht mehr.");

        db.OeffentlicheFaqEintraege.Remove(row);
        await CommitAsync(db, cancellationToken);
    }

    /// <summary>The one place this service writes; every change leaves through here and drops the snapshot.</summary>
    private async Task CommitAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    /// <summary>Sparse ordering; a new question goes to the end of its section.</summary>
    private static async Task<int> NextEntryOrderAsync(AppDbContext db, string rubrikId, CancellationToken cancellationToken)
    {
        var last = await db.OeffentlicheFaqEintraege
            .Where(e => e.RubrikId == rubrikId)
            .MaxAsync(e => (int?)e.SortOrder, cancellationToken) ?? 0;
        return Math.Min(9999, last + 10);
    }

    /// <summary>Moves one row by a step and renumbers the list; false when it was already at that end.</summary>
    /// <remarks>
    /// Renumbering rather than swapping two sort values: a swap is a silent no-op whenever two rows carry the same
    /// number, which sparse +10 numbering allows after enough edits.
    /// </remarks>
    private static bool Reorder<T>(List<T> ordered, string id, int delta, Func<T, string> idOf, Action<T, int> setOrder)
    {
        var from = ordered.FindIndex(x => string.Equals(idOf(x), id, StringComparison.Ordinal));
        var to = from + delta;
        if (from < 0 || to < 0 || to >= ordered.Count)
        {
            return false;
        }

        var row = ordered[from];
        ordered.RemoveAt(from);
        ordered.Insert(to, row);
        for (var i = 0; i < ordered.Count; i++)
        {
            setOrder(ordered[i], (i + 1) * 10);
        }
        return true;
    }

    /// <summary>Folds the question into a free anchor; the unique index is the backstop, not the check.</summary>
    private static async Task<string> MintAnchorAsync(AppDbContext db, string question, CancellationToken cancellationToken)
    {
        var basis = PublicPageSlug.Normalize(question);
        if (!PublicPageSlug.IsValid(basis))
        {
            // a question written entirely in punctuation or emoji still needs an address
            basis = "frage-" + Guid.NewGuid().ToString("N")[..8];
        }

        var taken = (await db.OeffentlicheFaqEintraege
            .Select(e => e.Anchor)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = basis;
        for (var n = 2; taken.Contains(candidate); n++)
        {
            var suffix = $"-{n}";
            var room = PublicPageSlug.MaxLength - suffix.Length;
            candidate = (basis.Length <= room ? basis : basis[..room].TrimEnd('-')) + suffix;
        }
        return candidate;
    }

    private async Task<PublicFaqSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PublicFaqSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        PublicFaqSnapshot snapshot;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            // the second gate, read here rather than in the caller so the public search inherits it: a question of
            // a retracted page would otherwise stay findable and link into "Seite nicht gefunden". The same row
            // carries the heading and the intro, so it is projected rather than counted.
            var head = await db.OeffentlicheSeiten
                .AsNoTracking()
                .Where(p => p.Slug == PublicFaq.PageSlug && p.Status == PublicPageStatus.Veroeffentlicht)
                .Select(p => new PublicFaqHead(p.Title, p.ContentHtml ?? string.Empty, p.PublishedAt, false))
                .FirstOrDefaultAsync(cancellationToken);
            snapshot = head is not null
                ? await BuildAsync(db, head, visibleOnly: true, cancellationToken)
                : PublicFaqSnapshot.Empty;
        }
        catch (Exception)
        {
            // an unreachable database shows no FAQ rather than a stack trace to an anonymous visitor
            return PublicFaqSnapshot.Empty;
        }

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private static async Task<PublicFaqSnapshot> BuildAsync(AppDbContext db, PublicFaqHead? head, bool visibleOnly, CancellationToken cancellationToken)
    {
        var rubriken = await db.OeffentlicheFaqRubriken
            .AsNoTracking()
            .Where(r => !visibleOnly || r.IsVisible)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Title)
            .ToListAsync(cancellationToken);
        if (rubriken.Count == 0)
        {
            // the heading survives an empty FAQ: the page is published, it just has nothing under it yet
            return new PublicFaqSnapshot([], head);
        }

        // flat WHERE ... IN, never a collection projection: Pomelo translates no LATERAL against MySQL
        var ids = rubriken.Select(r => r.Id).ToList();
        var entries = await db.OeffentlicheFaqEintraege
            .AsNoTracking()
            .Where(e => ids.Contains(e.RubrikId) && (!visibleOnly || e.IsVisible))
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Question)
            .ToListAsync(cancellationToken);
        var byRubrik = entries
            .GroupBy(e => e.RubrikId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var views = rubriken
            .Select(r => new PublicFaqRubrikView(
                r.Title,
                r.Description,
                PublicModules.IconFor(r.IconName, PublicFaq.RubrikDefaultIcon),
                r.DefaultOpen,
                (byRubrik.TryGetValue(r.Id, out var own) ? own : [])
                    .Select(e => new PublicFaqEntryView(
                        e.Anchor,
                        e.Question,
                        e.AnswerHtml ?? string.Empty,
                        // stripped once per cache fill, not once per anonymous search request
                        HtmlCleanup.PlainText(e.AnswerHtml),
                        !e.IsVisible))
                    .ToList(),
                !r.IsVisible))
            // outward a section with no question is a heading over nothing; the preview keeps it, so an empty
            // section its author just created is still visible to them
            .Where(r => !visibleOnly || r.Entries.Count > 0)
            .ToList();

        return new PublicFaqSnapshot(views, head);
    }

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    private static string? CutOrNull(string? value, int max) => value is null ? null : Cut(value, max);
}
