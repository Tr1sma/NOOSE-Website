namespace NOOSE_Website.Models.Common;

/// <summary>One soft-deleted record, flattened so every type shares the trash table.</summary>
/// <param name="Kind">Trash kind key; matches the section slug on the trash page.</param>
/// <param name="Reference">Aktenzeichen, or null for types that have none.</param>
/// <param name="Detail">Type-specific context, e.g. start and place of a meeting.</param>
public sealed record TrashItem(
    string Kind,
    string Id,
    string? Reference,
    string Title,
    string? Detail,
    DateTime? DeletedAt);

/// <summary>A record type that can be restored from the trash.</summary>
public sealed record TrashKind(string Key, string Label, string Icon, string ListRoute);
