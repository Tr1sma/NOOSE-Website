using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>Maintains automatic colleague links between people; each org kind keyed by its own label so they don't clobber each other.</summary>
public static class ColleaguesSync
{
    public const string FactionColleague = "Fraktionskollege";
    public const string GroupColleague = "Gruppenkollege";
    public const string PartyColleague = "Parteikollege";

    /// <summary>Reconciles the automatic links with the given label for one person; runs on the caller's context/transaction.</summary>
    /// <remarks>Both directions are considered, so syncing only P suffices. No unique index (would clash with soft-deleted manual links); automatic links are hard-deleted.</remarks>
    public static async Task SyncAsync(AppDbContext db, string personId, string label,
        IReadOnlyCollection<string> shouldColleagues, CancellationToken cancellationToken)
    {
        var shouldSet = shouldColleagues as HashSet<string> ?? shouldColleagues.ToHashSet();

        // AsNoTracking so the later SaveChanges for new links isn't disturbed by tracked entities.
        var existing = await db.Links.AsNoTracking()
            .Where(v => v.Automatic && v.Label == label && v.SourceType == nameof(Person) && v.TargetType == nameof(Person)
                     && (v.SourceId == personId || v.TargetId == personId))
            .Select(v => new { v.Id, v.SourceId, v.TargetId })
            .ToListAsync(cancellationToken);

        var have = new HashSet<string>();
        var toRemoveIds = new List<string>();
        foreach (var v in existing)
        {
            var other = v.SourceId == personId ? v.TargetId : v.SourceId;
            // Keep if still wanted and not yet seen; otherwise drop (including duplicates).
            if (shouldSet.Contains(other) && have.Add(other))
            {
                continue;
            }
            toRemoveIds.Add(v.Id);
        }

        if (toRemoveIds.Count > 0)
        {
            await db.Links.Where(v => toRemoveIds.Contains(v.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        var toSupplement = shouldSet.Where(q => !have.Contains(q)).ToList();
        foreach (var q in toSupplement)
        {
            db.Links.Add(new Link
            {
                SourceType = nameof(Person),
                SourceId = personId,
                TargetType = nameof(Person),
                TargetId = q,
                Label = label,
                Automatic = true,
            });
        }
        if (toSupplement.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
