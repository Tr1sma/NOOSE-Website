using MudBlazor;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Label and chip colour per audit action; shared by the log filter bar and the change table.</summary>
public static class AuditActionDisplay
{
    /// <summary>Actions the log filter offers, in reading order.</summary>
    public static readonly AuditAction[] All =
    [
        AuditAction.Created, AuditAction.Modified, AuditAction.Deleted, AuditAction.Restored,
    ];

    /// <summary>German label for an action.</summary>
    public static string Name(AuditAction action) => action switch
    {
        AuditAction.Created => "Erstellt",
        AuditAction.Modified => "Geändert",
        AuditAction.Deleted => "Gelöscht",
        AuditAction.Restored => "Wiederhergestellt",
        _ => action.ToString(),
    };

    /// <summary>Chip colour for an action.</summary>
    public static Color Colour(AuditAction action) => action switch
    {
        AuditAction.Created => Color.Success,
        AuditAction.Modified => Color.Info,
        AuditAction.Deleted => Color.Error,
        AuditAction.Restored => Color.Warning,
        _ => Color.Default,
    };
}
