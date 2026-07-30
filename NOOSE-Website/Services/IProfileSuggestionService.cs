using System.Security.Claims;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Shared autocomplete catalog for profile multi-fields; populated only from unclassified persons to keep classified values out of the shared list.</summary>
public interface IProfileSuggestionService
{
    /// <summary>Alphabetically sorted, distinct values of a type - the autocomplete source.</summary>
    Task<IReadOnlyList<string>> GetAsync(SuggestionType type, CancellationToken cancellationToken = default);

    /// <summary>Stages missing values onto the passed context without saving; the caller persists them atomically with the person. Existing values are skipped case-insensitively.</summary>
    Task StageAsync(AppDbContext db, SuggestionType type, IEnumerable<string> values, CancellationToken cancellationToken = default);

    /// <summary>All catalog values of a type with per-value usage counts across the mapped record tables (including trash).</summary>
    Task<IReadOnlyList<SuggestionEntry>> GetEntriesAsync(SuggestionType type, CancellationToken cancellationToken = default);

    /// <summary>Adds a value to a catalog; rejects duplicates case-insensitively. Leadership/admin with write access.</summary>
    Task CreateAsync(SuggestionType type, string value, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Renames a catalog value and propagates the rename to every record holding the old text (including trash). Leadership/admin with write access.</summary>
    Task RenameAsync(string entryId, string newValue, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Deletes a catalog value and removes it from every record: child rows are deleted, scalar fields nulled (including trash). Leadership/admin with write access.</summary>
    Task DeleteAsync(string entryId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Distinct activity kinds with usage counts; this list has no catalog, values live only on the activities themselves.</summary>
    Task<IReadOnlyList<SuggestionEntry>> GetActivityKindsAsync(CancellationToken cancellationToken = default);

    /// <summary>Renames an activity kind on all activities (including trash). Leadership/admin with write access.</summary>
    Task RenameActivityKindAsync(string oldKind, string newKind, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Clears an activity kind on all activities (including trash). Leadership/admin with write access.</summary>
    Task DeleteActivityKindAsync(string kind, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
