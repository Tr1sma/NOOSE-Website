namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Geltungsbereich einer Taskforce – Phase 5c. Legt fest, ob die Einsatzgruppe rein NOOSE-intern arbeitet
/// (<see cref="Innerbehoerdlich"/>) oder behördenübergreifend mit Partnern (DoJ/LSPD/LSMD) zusammengesetzt ist
/// (<see cref="Ueberbehoerdlich"/>).
/// </summary>
public enum TaskforceGeltungsbereich
{
    /// <summary>Innerbehördlich – ausschließlich NOOSE.</summary>
    Innerbehoerdlich = 0,
    /// <summary>Überbehördlich – behördenübergreifend (mit Partnerbehörden).</summary>
    Ueberbehoerdlich = 1,
}

/// <summary>Anzeigetexte für den Taskforce-Geltungsbereich (UI-frei).</summary>
public static class TaskforceGeltungsbereichAnzeige
{
    public static string Name(TaskforceGeltungsbereich bereich) => bereich switch
    {
        TaskforceGeltungsbereich.Innerbehoerdlich => "Innerbehördlich",
        TaskforceGeltungsbereich.Ueberbehoerdlich => "Überbehördlich",
        _ => "—",
    };

    public static readonly IReadOnlyList<TaskforceGeltungsbereich> Alle = new[]
    {
        TaskforceGeltungsbereich.Innerbehoerdlich,
        TaskforceGeltungsbereich.Ueberbehoerdlich,
    };
}
