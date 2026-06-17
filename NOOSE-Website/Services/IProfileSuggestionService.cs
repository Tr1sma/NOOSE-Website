using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Shared autocomplete catalog for profile multi-fields; populated only from unclassified persons to keep classified values out of the shared list.</summary>
public interface IProfileSuggestionService
{
    /// <summary>Alphabetically sorted, distinct values of a type - the autocomplete source.</summary>
    Task<IReadOnlyList<string>> GetAsync(SuggestionType type, CancellationToken cancellationToken = default);

    /// <summary>Stages missing values onto the passed context without saving; the caller persists them atomically with the person. Existing values are skipped case-insensitively.</summary>
    Task StageAsync(AppDbContext db, SuggestionType type, IEnumerable<string> values, CancellationToken cancellationToken = default);
}
