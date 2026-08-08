namespace NOOSE_Website.Models.Enums;

/// <summary>What an agent did to a record. Read comes from the access log, the rest from the audit log.</summary>
public enum CounterIntelActionKind
{
    Read = 0,
    Created = 1,
    Modified = 2,
    Deleted = 3,
    Restored = 4,
}

/// <summary>Display labels.</summary>
public static class CounterIntelActionKindDisplay
{
    public static string Name(CounterIntelActionKind kind) => kind switch
    {
        CounterIntelActionKind.Read => "Gelesen",
        CounterIntelActionKind.Created => "Erstellt",
        CounterIntelActionKind.Modified => "Geändert",
        CounterIntelActionKind.Deleted => "Gelöscht",
        CounterIntelActionKind.Restored => "Wiederhergestellt",
        _ => kind.ToString(),
    };

    public static readonly IReadOnlyList<CounterIntelActionKind> All =
    [
        CounterIntelActionKind.Read,
        CounterIntelActionKind.Created,
        CounterIntelActionKind.Modified,
        CounterIntelActionKind.Deleted,
        CounterIntelActionKind.Restored,
    ];

    /// <summary>Maps an audit action onto the rule vocabulary.</summary>
    public static CounterIntelActionKind From(AuditAction action) => action switch
    {
        AuditAction.Created => CounterIntelActionKind.Created,
        AuditAction.Modified => CounterIntelActionKind.Modified,
        AuditAction.Deleted => CounterIntelActionKind.Deleted,
        _ => CounterIntelActionKind.Restored,
    };
}
