namespace NOOSE_Website.Navigation;

/// <summary>Section slugs of the pages that absorbed others in V1.5; keeps LegacyRoutes and the pages in sync.</summary>
public static class MergedPageSections
{
    public static readonly string[] Settings =
    [
        "system", "discord", "status",
        "custom-felder", "tags", "aktualitaet", "bedrohungs-score",
        "vorlagen-dok", "vorlagen-dokument", "vorlagen-aktivitaet", "vorlagen-personal",
        "module", "einladungen",
        "partner",
        "basisdaten",
    ];

    public static readonly string[] Monitoring =
    [
        "chronik",
        "aenderungen", "zugriffe",
        "lagebild", "agenten-profil", "auffaelligkeiten", "regeln",
    ];

    public static readonly string[] Trash =
    [
        "personen", "fraktionen", "personengruppen", "parteien",
        "vorgaenge", "operationen", "taskforces", "aufgaben",
        "brett", "kalender", "besprechungen", "aktivitaeten", "abmeldungen",
    ];

    public static readonly string[] Wanted = ["fahndung", "observationen", "doks"];

    public static readonly string[] Absences = ["meine", "uebersicht", "papierkorb"];

    public static readonly string[] Recruiting = ["eingang", "sperren", "vorlagen", "tests"];

    public static readonly string[] Statistics =
    [
        "ueberblick", "bestand", "aktivitaet", "bedrohung", "netzwerk", "dienststelle", "lageberichte",
    ];

    /// <summary>Section slugs of a merged page, keyed by its route.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ByRoute =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["/einstellungen"] = Settings,
            ["/papierkorb"] = Trash,
            ["/fahndung"] = Wanted,
            ["/abmeldungen"] = Absences,
            ["/bewerbungen"] = Recruiting,
            ["/statistik"] = Statistics,
            ["/nachweis"] = Monitoring,
        };
}
