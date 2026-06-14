namespace NOOSE_Website.Authorization;

/// <summary>
/// Namens-Konstanten aller Authorization-Policies (siehe Rechte-Matrix in <c>Plan.md</c> §6).
/// Verwendung: <c>[Authorize(Policy = Policies.Fuehrung)]</c> bzw. <c>&lt;AuthorizeView Policy="..."&gt;</c>.
/// </summary>
public static class Policies
{
    /// <summary>Eingeloggt und Status = Aktiv. App-weiter Standard (in <c>_Imports.razor</c>).</summary>
    public const string ActiveAgent = "AktiverAgent";

    /// <summary>Führung: Dienstgrad ≥ Supervisory Special Agent oder Admin.</summary>
    public const string Leadership = "Fuehrung";

    /// <summary>Technische Systemrolle.</summary>
    public const string Admin = "Admin";

    /// <summary>Darf überhaupt schreiben (alle außer der Nur-Lese-Aufsicht = TeamLeitung ohne Admin). Für
    /// <c>AuthorizeView</c> um Mutations-Controls (Anlegen/Bearbeiten/Speichern).</summary>
    public const string WriteAccess = "Schreibrecht";

    /// <summary>Nur-Lese-Aufsicht aktiv (TeamLeitung ohne Admin). Für den globalen Nur-Lese-Hinweis-Banner.</summary>
    public const string OnlyReadMode = "NurLeseModus";

    /// <summary>Seiten-Zugang Führungsbereich: Führung ODER Nur-Lese-Aufsicht (öffnet die Seite read-only,
    /// ohne Schreib-Buttons – diese bleiben an <see cref="Fuehrung"/> gebunden).</summary>
    public const string LeadershipPage = "FuehrungSeite";

    /// <summary>Seiten-Zugang „höchste Einstufung"-Bereich (Freigaben): wie <see cref="HoechsteEinstufung"/>
    /// plus Nur-Lese-Aufsicht.</summary>
    public const string HighestClassificationPage = "HoechsteEinstufungSeite";

    /// <summary>Seiten-Zugang Admin-Bereich: Admin ODER Nur-Lese-Aufsicht (öffnet die Seite read-only).</summary>
    public const string AdminPage = "AdminSeite";

    /// <summary>"Gesichert staatsgefährdend" direkt setzen: Dienstgrad ≥ Senior Special Agent oder Admin.</summary>
    public const string HighestClassification = "HoechsteEinstufung";

    /// <summary>Beförderung entscheiden: Dienstgrad ≥ Deputy Director oder Admin.</summary>
    public const string PromotionDecide = "BefoerderungEntscheiden";

    /// <summary>
    /// Verschlusssachen sehen. Derzeit ungenutzt: Die VS-Durchsetzung läuft serverseitig in der
    /// Service-Schicht (siehe <c>Sichtbarkeit</c> und die VS-Guards). Diese Policy bleibt als künftiges
    /// ressourcenbasiertes UI-Gate reserviert (volle Prüfung inkl. ausdrücklicher Agent-Zuweisung).
    /// </summary>
    public const string Classified = "Verschlusssache";
}
