using System.Security.Claims;
using NOOSE_Website.Data.Entities.Changelog;
using NOOSE_Website.Models.Changelog;

namespace NOOSE_Website.Services.Changelog;

/// <summary>Reads and edits the user-facing list of changes shown on <c>/neuerungen</c>.</summary>
public interface IChangelogService
{
    /// <summary>Visible releases newest first, each with its visible lines; releases without lines are dropped.</summary>
    Task<List<ChangelogReleaseView>> GetTimelineAsync(CancellationToken cancellationToken = default);

    /// <summary>How much arrived since the agent last looked. Null stamp means "first visit" and counts nothing.</summary>
    Task<ChangelogNewsFlash> GetNewsSinceAsync(DateTime? lastSeenUtc, CancellationToken cancellationToken = default);

    /// <summary>Every release including hidden ones, newest first; for the editor.</summary>
    Task<List<ChangelogRelease>> GetReleasesAsync(CancellationToken cancellationToken = default);

    /// <summary>Every line of one release including hidden ones, in order; for the editor.</summary>
    Task<List<ChangelogEntry>> GetEntriesAsync(string releaseId, CancellationToken cancellationToken = default);

    /// <summary>Every line of the given releases including hidden ones, grouped by release; for the editor.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<ChangelogEntry>>> GetEntriesAsync(
        IReadOnlyCollection<string> releaseIds, CancellationToken cancellationToken = default);

    Task<ChangelogRelease> CreateReleaseAsync(ChangelogReleaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshReleaseAsync(string id, ChangelogReleaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<ChangelogEntry> CreateEntryAsync(ChangelogEntryInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshEntryAsync(string id, ChangelogEntryInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<ChangelogEntry>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
