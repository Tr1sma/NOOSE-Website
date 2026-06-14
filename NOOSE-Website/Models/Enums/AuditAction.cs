namespace NOOSE_Website.Models.Enums;

/// <summary>Art einer protokollierten Änderung im Änderungs-Log (<c>AuditLog</c>).</summary>
public enum AuditAction
{
    Created = 0,
    Modified = 1,
    Deleted = 2,
    Restored = 3,
}
