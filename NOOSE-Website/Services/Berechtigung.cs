using System.Security.Claims;
using NOOSE_Website.Authorization;

namespace NOOSE_Website.Services;

/// <summary>
/// Serverseitige Durchsetzung der Rechte-Matrix (Plan.md §6) in der Service-Schicht. <c>AuthorizeView</c>
/// in Razor versteckt nur die UI – die schreibenden Dienste müssen Rang/Eigentum selbst prüfen. Diese
/// Guards werfen <see cref="UnauthorizedAccessException"/> mit einer für den Nutzer verständlichen
/// Meldung (die UI fängt Service-Ausnahmen ab und zeigt sie als Snackbar).
/// </summary>
public static class Berechtigung
{
    /// <summary>Wirft, wenn der Handelnde nicht der Führung (Supervisory Special Agent+) oder Admin angehört.</summary>
    public static void VerlangeFuehrung(ClaimsPrincipal handelnder)
    {
        if (!handelnder.IstFuehrung())
        {
            throw new UnauthorizedAccessException(
                "Diese Aktion ist der Führung (ab Supervisory Special Agent) oder Admins vorbehalten.");
        }
    }

    /// <summary>Wirft, wenn der Handelnde Beförderungen nicht entscheiden darf (Deputy Director+ oder Admin).</summary>
    public static void VerlangeBefoerderungEntscheiden(ClaimsPrincipal handelnder)
    {
        if (!handelnder.DarfBefoerderungEntscheiden())
        {
            throw new UnauthorizedAccessException(
                "Über Beförderungen entscheidet nur Deputy Director aufwärts oder ein Admin.");
        }
    }

    /// <summary>Wirft, wenn der Handelnde die höchste Einstufung nicht setzen/entscheiden darf
    /// (Senior Special Agent+ oder Admin) – u. a. die Entscheidung über Hochstufungs-Anträge.</summary>
    public static void VerlangeHoechsteEinstufung(ClaimsPrincipal handelnder)
    {
        if (!handelnder.DarfHoechsteEinstufung())
        {
            throw new UnauthorizedAccessException(
                "Über Hochstufungen auf „Gesichert staatsgefährdend“ entscheidet nur Senior Special Agent aufwärts oder ein Admin.");
        }
    }
}
