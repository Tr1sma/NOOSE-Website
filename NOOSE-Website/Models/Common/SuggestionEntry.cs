using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Admin view of one value-list row: key (catalog id or the value itself), optional suggestion type, text and how many records currently use it.</summary>
public record SuggestionEntry(string Id, SuggestionType? Type, string Value, int UsageCount);
