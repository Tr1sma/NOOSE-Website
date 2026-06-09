namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus eines Agent-Accounts. Nur <see cref="Aktiv"/> erhaelt beim Login
/// tatsaechlich eine Sitzung; <see cref="Ausstehend"/> wartet auf Freigabe durch
/// Fuehrung/Admin, <see cref="Gesperrt"/> ist per Notfall-Sperre deaktiviert.
/// </summary>
public enum AgentStatus
{
    Ausstehend = 0,
    Aktiv = 1,
    Gesperrt = 2,
}
