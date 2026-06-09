namespace NOOSE_Website.Authorization;

/// <summary>
/// Namens-Konstanten aller Authorization-Policies (siehe Rechte-Matrix in <c>Plan.md</c> §6).
/// Verwendung: <c>[Authorize(Policy = Policies.Fuehrung)]</c> bzw. <c>&lt;AuthorizeView Policy="..."&gt;</c>.
/// </summary>
public static class Policies
{
    /// <summary>Eingeloggt und Status = Aktiv. App-weiter Standard (in <c>_Imports.razor</c>).</summary>
    public const string AktiverAgent = "AktiverAgent";

    /// <summary>Führung: Dienstgrad ≥ Supervisory Special Agent oder Admin.</summary>
    public const string Fuehrung = "Fuehrung";

    /// <summary>Technische Systemrolle.</summary>
    public const string Admin = "Admin";

    /// <summary>"Gesichert staatsgefährdend" direkt setzen: Dienstgrad ≥ Senior Special Agent oder Admin.</summary>
    public const string HoechsteEinstufung = "HoechsteEinstufung";

    /// <summary>Beförderung entscheiden: Dienstgrad ≥ Deputy Director oder Admin.</summary>
    public const string BefoerderungEntscheiden = "BefoerderungEntscheiden";

    /// <summary>Verschlusssachen sehen (Stub – volle ressourcenbasierte Prüfung in späterer Phase).</summary>
    public const string Verschlusssache = "Verschlusssache";
}
