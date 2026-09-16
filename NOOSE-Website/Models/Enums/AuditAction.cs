namespace NOOSE_Website.Models.Enums;

/// <summary>Audit log action type.</summary>
public enum AuditAction
{
    Created = 0,
    Modified = 1,
    Deleted = 2,
    Restored = 3,
    /// <summary>Filed away: out of listings, still readable.</summary>
    Archived = 4,
    /// <summary>Brought back into the active stock.</summary>
    Unarchived = 5,
}
