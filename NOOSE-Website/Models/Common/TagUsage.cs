using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Models.Common;

/// <summary>A tag with its current assignment count.</summary>
public record TagUsage(Tag Tag, int Count);
