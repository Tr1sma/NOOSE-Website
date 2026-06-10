namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Priorität einer Aufgabe/To-Do – Phase 6. Steuert Sortierung und Chip-Farbe. Default ist <see cref="Normal"/>.
/// </summary>
public enum AufgabePrioritaet
{
    Niedrig = 0,
    Normal = 1,
    Hoch = 2,
}

/// <summary>Anzeigetexte für die Aufgaben-Priorität (UI-frei, ohne MudBlazor-Abhängigkeit; Farbe im Chip).</summary>
public static class AufgabePrioritaetAnzeige
{
    public static string Name(AufgabePrioritaet prioritaet) => prioritaet switch
    {
        AufgabePrioritaet.Niedrig => "Niedrig",
        AufgabePrioritaet.Normal => "Normal",
        AufgabePrioritaet.Hoch => "Hoch",
        _ => "—",
    };

    public static readonly IReadOnlyList<AufgabePrioritaet> Alle = new[]
    {
        AufgabePrioritaet.Niedrig,
        AufgabePrioritaet.Normal,
        AufgabePrioritaet.Hoch,
    };
}
