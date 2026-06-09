namespace NOOSE_Website.Models.Enums;

/// <summary>Anzeigetexte für den Lebensstatus (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class LebensstatusAnzeige
{
    public static string Name(Lebensstatus status) => status switch
    {
        Lebensstatus.Lebend => "Lebend",
        Lebensstatus.Tot => "Tot",
        Lebensstatus.Fluechtig => "Flüchtig",
        _ => "—",
    };

    public static readonly IReadOnlyList<Lebensstatus> Alle = new[]
    {
        Lebensstatus.Lebend,
        Lebensstatus.Tot,
        Lebensstatus.Fluechtig,
    };
}

/// <summary>Anzeigetexte für die Sicherheitseinstufung.</summary>
public static class EinstufungAnzeige
{
    public static string Name(Einstufung einstufung) => einstufung switch
    {
        Einstufung.Unbekannt => "Unbekannt",
        Einstufung.Prueffall => "Prüffall",
        Einstufung.Verdachtsfall => "Verdachtsfall",
        Einstufung.GesichertStaatsgefaehrdend => "Gesichert staatsgefährdend",
        _ => "—",
    };

    public static readonly IReadOnlyList<Einstufung> Alle = new[]
    {
        Einstufung.Unbekannt,
        Einstufung.Prueffall,
        Einstufung.Verdachtsfall,
        Einstufung.GesichertStaatsgefaehrdend,
    };
}

/// <summary>Anzeigetexte für den Maßnahme-Ausgang.</summary>
public static class MassnahmeAusgangAnzeige
{
    public static string Name(MassnahmeAusgang ausgang) => ausgang switch
    {
        MassnahmeAusgang.LaeuftNoch => "Läuft noch",
        MassnahmeAusgang.OffiziellEntlassen => "Offiziell entlassen",
        MassnahmeAusgang.Spritze => "Amnestie-Spritze",
        MassnahmeAusgang.Erschossen => "Erschossen",
        _ => "—",
    };

    public static readonly IReadOnlyList<MassnahmeAusgang> Alle = new[]
    {
        MassnahmeAusgang.LaeuftNoch,
        MassnahmeAusgang.OffiziellEntlassen,
        MassnahmeAusgang.Spritze,
        MassnahmeAusgang.Erschossen,
    };
}
