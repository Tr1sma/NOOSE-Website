using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicFactionProfileService" />
/// <remarks>
/// Takes no <c>IFactionService</c> on purpose: the dependency runs the other way, because a faction file that becomes
/// a Verschlusssache has to pull its own profile offline.
/// </remarks>
public class PublicFactionProfileService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IMemoryCache cache) : IPublicFactionProfileService
{
    private const string CacheKey = "OeffentlicheFraktionsprofile";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    private const int MaxDisplayName = 200;

    private const string NotFound = "Organisationsprofil nicht gefunden.";
    private const string RecordNotFound = "Akte nicht gefunden.";
    private const string Classified = "Eine Verschlusssache wird nicht öffentlich dargestellt.";

    // ---- outward reads ----

    public async Task<PublicFactionBoard> GetBoardAsync(CancellationToken cancellationToken = default)
    {
        if (!await modules.IsEnabledAsync(PublicModules.Organisations, cancellationToken))
        {
            return PublicFactionBoard.Empty;
        }
        return await LoadAsync(cancellationToken);
    }

    // ---- internal reads ----

    public async Task<PublicFactionProfileBanner?> GetBannerForFactionAsync(string factionId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheFraktionsprofile
            .AsNoTracking()
            .Where(p => p.FactionId == factionId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PublicFactionProfileBanner(p.Status, p.Standing, p.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicFactionProfileEdit>> GetAllAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // rooted, with !IsDeleted written back by hand: ProjectAsync dereferences the REQUIRED Faction navigation,
        // so a profile whose file was deleted would be INNER-joined out of the list and never reach the gate
        // below - which exists precisely to keep a leftover profile manageable
        var rows = await ProjectAsync(
            db.OeffentlicheFraktionsprofile.IgnoreQueryFilters().AsNoTracking().Where(p => !p.IsDeleted),
            cancellationToken);
        // the profile carries the content of a file, so the read gate of the file applies to it
        var visible = await VisibleFactionsAsync(db, rows.Select(r => r.FactionId), actor, cancellationToken);
        return rows.Where(r => visible.Contains(r.FactionId)).ToList();
    }

    public async Task<PublicFactionProfileEdit?> GetForFactionAsync(string factionId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // rooted for the same reason as GetAllAsync
        var rows = await ProjectAsync(
            db.OeffentlicheFraktionsprofile.IgnoreQueryFilters().AsNoTracking()
                .Where(p => !p.IsDeleted && p.FactionId == factionId),
            cancellationToken);
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return null;
        }
        var visible = await VisibleFactionsAsync(db, [row.FactionId], actor, cancellationToken);
        return visible.Contains(row.FactionId) ? row : null;
    }

    public async Task<PublicFactionProfileDraft?> GetDraftAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileRead(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }
        var visible = await VisibleFactionsAsync(db, [row.FactionId], actor, cancellationToken);
        return visible.Contains(row.FactionId)
            ? new PublicFactionProfileDraft(row.Id, row.FactionId, row.Status, row.DisplayName, row.Standing,
                row.DescriptionHtml)
            : null;
    }

    // ---- writes ----

    public async Task<string> CreateDraftFromFactionAsync(string factionId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == factionId, cancellationToken)
            ?? throw new InvalidOperationException(RecordNotFound);
        RequireNotClassified(faction, actor);

        // One live profile per faction, checked over the living rows instead of by a unique index: with soft delete an
        // index would block the faction forever. The query filter drops the deleted rows, which is exactly right —
        // a profile in the bin must not block the address.
        var exists = await db.OeffentlicheFraktionsprofile
            .AnyAsync(p => p.FactionId == factionId, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Diese Organisation hat bereits ein Profil.");
        }

        var row = new OeffentlichesFraktionsprofil
        {
            FactionId = factionId,
            DisplayName = Cut(faction.Name, MaxDisplayName),
            Standing = PublicFactionStanding.Beobachtet,
            Status = PublicProfileStatus.Entwurf,
        };
        db.OeffentlicheFraktionsprofile.Add(row);
        await SaveAndInvalidateAsync(db, cancellationToken);
        return row.Id;
    }

    public async Task UpdateSnapshotAsync(PublicFactionProfileInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile.FirstOrDefaultAsync(p => p.Id == input.Id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        // always, not only for a live profile: whoever knows the id of a draft must not be able to write against a
        // Verschlusssache and leave an audit row on it
        await RequireVisibleFactionAsync(db, row.FactionId, actor, cancellationToken);

        var live = row.Status == PublicProfileStatus.Veroeffentlicht;

        var name = (input.DisplayName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Das Profil braucht einen Anzeigenamen.");
        }
        row.DisplayName = Cut(name, MaxDisplayName);
        row.Standing = RequireKnownStanding(input.Standing);

        // null means "leave the stored description alone", "" means "clear it" — without the split, saving a renamed
        // profile would silently drop its text
        if (input.DescriptionHtml is not null)
        {
            row.DescriptionHtml = HtmlCleanup.Clean(input.DescriptionHtml);
        }

        if (live)
        {
            RequirePublishableContent(row);
        }

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // first, so a read-only supervisor does not even learn whether the module is on
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);
        await modules.RequireEnabledAsync(PublicModules.Organisations, cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        if (row.Status == PublicProfileStatus.Veroeffentlicht)
        {
            throw new InvalidOperationException("Das Profil ist bereits veröffentlicht.");
        }

        var faction = await RequirePublishableRecordAsync(db, row, actor, cancellationToken);
        // cleaned here as well as on save: this is the moment the markup becomes anonymously reachable
        row.DescriptionHtml = HtmlCleanup.Clean(row.DescriptionHtml);
        RequirePublishableContent(row);

        row.Status = PublicProfileStatus.Veroeffentlicht;
        row.PublishedAt = DateTime.UtcNow;
        row.PublishedById = actor.GetAgentId();
        row.RetractedAt = null;
        row.RetractedReason = null;
        // the level, never the raw score: the 0-100 value would be the last reason to read Fraktionen for content
        row.PublicHazardLevel = HazardLevelLogic.From(faction.ThreatScore);

        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task RetractAsync(string id, string reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Zum Zurückziehen gehört ein Grund.");
        }

        // deliberately no module gate: publishing needs a live module, taking something offline never does — otherwise
        // the kill switch would make it impossible to pull an entry, exactly backwards
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        if (row.Status != PublicProfileStatus.Veroeffentlicht)
        {
            throw new InvalidOperationException("Nur ein veröffentlichtes Profil lässt sich zurückziehen.");
        }

        RetractRow(row, reason.Trim());
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task RefreshHazardLevelAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        var faction = await RequirePublishableRecordAsync(db, row, actor, cancellationToken);

        row.PublicHazardLevel = HazardLevelLogic.From(faction.ThreatScore);
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);
        // deleting a live profile would be a silent depublication with no reason on the record
        if (row.Status == PublicProfileStatus.Veroeffentlicht)
        {
            throw new InvalidOperationException("Ein veröffentlichtes Profil muss erst zurückgezogen werden.");
        }

        db.OeffentlicheFraktionsprofile.Remove(row);
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    public async Task RetractForRecordAsync(string factionId, string reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        // no guard and no module gate on purpose: the caller is a record write path that passed its own, and pulling
        // something offline must work even while the public area is switched off
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.OeffentlicheFraktionsprofile
            .Where(p => p.FactionId == factionId && p.Status == PublicProfileStatus.Veroeffentlicht)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            RetractRow(row, reason);
        }
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    // ---- trash ----

    public async Task<List<OeffentlichesFraktionsprofil>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.OeffentlicheFraktionsprofile
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequirePublicFactionProfileWrite(actor);
        Permission.RequireHighestClassification(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.OeffentlicheFraktionsprofile
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException(NotFound);

        // the faction may have gained a profile in the meantime; two live ones on one file is what the missing unique
        // index cannot prevent, so restoring has to ask
        var taken = await db.OeffentlicheFraktionsprofile
            .AnyAsync(p => p.FactionId == row.FactionId, cancellationToken);
        if (taken)
        {
            throw new InvalidOperationException("Die Organisation hat inzwischen ein anderes Profil.");
        }

        // the file may have become a Verschlusssache while the profile sat in the bin
        await RequirePublishableRecordAsync(db, row, actor, cancellationToken);

        row.IsDeleted = false;
        row.DeletedAt = null;
        row.DeletedById = null;
        // back as a draft, so an undo never republishes something on the way
        row.Status = PublicProfileStatus.Entwurf;
        row.PublishedAt = null;
        row.PublishedById = null;
        await SaveAndInvalidateAsync(db, cancellationToken);
    }

    // ---- internals ----

    /// <summary>The one save path of this table: nothing writes it without dropping the snapshot.</summary>
    /// <remarks>
    /// A file scan holds this shape (<c>PublicFactionProfileCacheDisciplineTests</c>). Own cache key rather than
    /// sharing the wanted board's: a different table, invalidated by different writes.
    /// </remarks>
    private async Task SaveAndInvalidateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
    }

    private async Task<PublicFactionBoard> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out PublicFactionBoard? cached) && cached is not null)
        {
            return cached;
        }

        PublicFactionBoard board;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.OeffentlicheFraktionsprofile
                .AsNoTracking()
                .Where(p => p.Status == PublicProfileStatus.Veroeffentlicht)
                .OrderByDescending(p => p.PublishedAt)
                .Select(p => new
                {
                    p.FactionId,
                    p.DisplayName,
                    p.Standing,
                    p.PublicHazardLevel,
                    p.DescriptionHtml,
                    p.PublishedAt,
                })
                .ToListAsync(cancellationToken);

            // The suppression belt, as a second query rather than a subquery: IgnoreQueryFilters is
            // compilation-scoped, so a subquery using it strips !IsDeleted from the OUTER set as well and a deleted
            // profile goes live. p.Faction as a navigation is just as unusable — it inherits the filter and is null
            // for a deleted file, so "p.Faction == null || ..." would show exactly the rows it must hide.
            var open = await OpenFactionsAsync(db, rows.Select(r => r.FactionId), cancellationToken);

            var cards = rows
                .Where(r => open.Contains(r.FactionId))
                .Select(r => new PublicFactionCard(r.DisplayName, r.Standing, r.PublicHazardLevel, r.DescriptionHtml,
                    r.PublishedAt))
                .ToList();
            // stripped once per cache fill, not once per anonymous search request
            board = new PublicFactionBoard(
                cards,
                cards.Select(c => HtmlCleanup.PlainText(c.DescriptionHtml)).ToList());
        }
        catch (Exception)
        {
            // never cache a failure: the next request should try again rather than sit on an empty hub
            return PublicFactionBoard.Empty;
        }

        cache.Set(CacheKey, board, CacheDuration);
        return board;
    }

    /// <summary>Rows of the management list, newest first; no description, so a list render stays small.</summary>
    private static async Task<List<PublicFactionProfileEdit>> ProjectAsync(
        IQueryable<OeffentlichesFraktionsprofil> query, CancellationToken cancellationToken)
        => await query
            .OrderByDescending(p => p.CreatedAt)
            // the codename, not the identity user: a RealName has no business in a panel the supervision renders
            .Select(p => new PublicFactionProfileEdit(p.Id, p.FactionId, p.DisplayName, p.Faction!.CaseNumber,
                p.Standing, p.Status, p.PublicHazardLevel, p.PublishedAt, p.PublishedBy!.Codename, p.ModifiedAt))
            .ToListAsync(cancellationToken);

    /// <summary>Of the given faction files, the ones that may carry anything outside at all.</summary>
    private static async Task<HashSet<string>> OpenFactionsAsync(AppDbContext db, IEnumerable<string> factionIds,
        CancellationToken cancellationToken)
    {
        var ids = Ids(factionIds);
        if (ids.Count == 0)
        {
            return Empty();
        }

        return (await db.Factions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(f => ids.Contains(f.Id) && !f.IsDeleted
                    && !f.IsClassified && !f.IsTRUClassified && !f.IsHRBClassified)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Of the given faction files, the ones this actor may read.</summary>
    /// <remarks>
    /// A deleted file still resolves its secrecy level here, so a leftover profile stays manageable by whoever may see
    /// that level. A file that does not resolve at all is not visible — fail closed.
    /// </remarks>
    private static async Task<HashSet<string>> VisibleFactionsAsync(AppDbContext db, IEnumerable<string> factionIds,
        ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var ids = Ids(factionIds);
        if (ids.Count == 0)
        {
            return Empty();
        }

        var scope = ViewerScope.From(actor);
        var rows = await db.Factions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.IsClassified, f.IsTRUClassified, f.IsHRBClassified })
            .ToListAsync(cancellationToken);

        return rows
            .Where(f => RecordVisibility.IsVisible(scope, f.IsClassified, f.IsTRUClassified, f.IsHRBClassified))
            .Select(f => f.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task RequireVisibleFactionAsync(AppDbContext db, string factionId, ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        var visible = await VisibleFactionsAsync(db, [factionId], actor, cancellationToken);
        if (!visible.Contains(factionId))
        {
            throw new InvalidOperationException(NotFound);
        }
    }

    /// <summary>Loads the file behind a profile and refuses a classified or deleted one, rank-independently.</summary>
    private static async Task<Faction> RequirePublishableRecordAsync(AppDbContext db,
        OeffentlichesFraktionsprofil row, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        // no IgnoreQueryFilters: a soft-deleted file blocks publication the same way a missing one does
        var faction = await db.Factions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == row.FactionId, cancellationToken)
            ?? throw new InvalidOperationException(RecordNotFound);
        RequireNotClassified(faction, actor);
        return faction;
    }

    /// <summary>All three secrecy flags block, admin included; the message depends on who is asking.</summary>
    private static void RequireNotClassified(Faction faction, ClaimsPrincipal actor)
    {
        if (!faction.IsClassified && !faction.IsTRUClassified && !faction.IsHRBClassified)
        {
            return;
        }
        // to someone who may not read classified records, the refusal reads exactly like a missing file — otherwise
        // pressing publish would tell the actor that the record has since become a Verschlusssache
        throw new InvalidOperationException(actor.MayClassifiedRead() ? Classified : RecordNotFound);
    }

    /// <summary>Everything a profile must satisfy before it may go outside.</summary>
    private static void RequirePublishableContent(OeffentlichesFraktionsprofil row)
    {
        if (string.IsNullOrWhiteSpace(row.DisplayName))
        {
            throw new InvalidOperationException("Das Profil braucht einen Anzeigenamen.");
        }
        RequirePublishableDescription(row.DescriptionHtml);
    }

    /// <summary>Refuses, never strips: silently removing part of a description changes what it says.</summary>
    private static void RequirePublishableDescription(string? html)
    {
        var text = HtmlCleanup.PlainText(html);
        if (text.Length == 0 && !(html ?? string.Empty).Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Das Profil braucht eine Kurzbeschreibung.");
        }
        if (MentionParser.Parse(html).Count > 0)
        {
            throw new InvalidOperationException(
                "Die Kurzbeschreibung enthält eine Erwähnung; öffentlicher Text darf keine Akte verlinken.");
        }
        // the bare opener, like the warning label: "{{Name" without its closing pair is no token of any system here
        // and would travel outside verbatim
        if ((html ?? string.Empty).Contains("{{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Die Kurzbeschreibung enthält einen Platzhalter.");
        }
    }

    private static void RetractRow(OeffentlichesFraktionsprofil row, string reason)
    {
        // name, label and description stay: visibility hangs on the status, so going back online is one click
        row.Status = PublicProfileStatus.Zurueckgezogen;
        row.RetractedAt = DateTime.UtcNow;
        row.RetractedReason = reason;
    }

    /// <summary>An allowlist, never a cast: a value off the enum would render as a raw number outside.</summary>
    private static PublicFactionStanding RequireKnownStanding(PublicFactionStanding standing)
        => PublicFactionStandingDisplay.All.Contains(standing)
            ? standing
            : throw new InvalidOperationException("Unbekannte Einordnung.");

    private static List<string> Ids(IEnumerable<string> factionIds)
        => factionIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();

    private static HashSet<string> Empty() => new(StringComparer.Ordinal);

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
