namespace NOOSE_Website.Models.Enums;

/// <summary>Which part of the stock a listing asks for.</summary>
public enum ArchiveFilter
{
    /// <summary>Active stock only. The default everywhere.</summary>
    Active = 0,
    /// <summary>Active and archived together.</summary>
    Including = 1,
    /// <summary>Archived only.</summary>
    Only = 2,
}
