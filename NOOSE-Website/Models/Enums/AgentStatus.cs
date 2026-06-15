namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus eines Agent-Accounts. Nur <see cref="Aktiv"/> erhält beim Login
/// tatsächlich eine Sitzung; <see cref="Ausstehend"/> wartet auf Freigabe durch
/// Führung/Admin, <see cref="Gesperrt"/> ist per Notfall-Sperre deaktiviert.
/// </summary>
public enum AgentStatus
{
    Pending = 0,
    Active = 1,
    Blocked = 2,
}
