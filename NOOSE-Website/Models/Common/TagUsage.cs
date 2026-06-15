using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Models.Common;

/// <summary>Ein Tag samt Anzahl seiner aktuellen Zuordnungen (für die Tag-Verwaltung <c>/tags</c>).</summary>
public record TagUsage(Tag Tag, int Count);
